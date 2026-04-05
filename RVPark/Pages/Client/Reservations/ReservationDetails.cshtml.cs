using ApplicationCore.Dtos;
using ApplicationCore.Enums;
using ApplicationCore.Models;
using Infrastructure.Data;
using Infrastructure.Services;
using Infrastructure.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace RVPark.Pages.Client.Reservations
{
    public class ReservationDetailsModel : PageModel
    {
        private readonly UnitOfWork _unitOfWork;
        private readonly TransactionService _transactionService;
        private readonly ApplicationDbContext _dbContext;
        private readonly IConfiguration _config;
        private readonly IEnumerable<Fee> _transactionTypes;

        public Reservation Reservation { get; set; } = new();
        public List<Transaction> AllTransactions { get; set; } = new();
        public List<TransactionSummaryDto> TransactionSummaries { get; set; } = new();
        public List<TransactionGroupDto> GroupedTransactions { get; set; } = new();
        public Dictionary<int, string> TransactionBreakdowns { get; set; } = new();
        public Dictionary<int, string> TransactionStatuses { get; set; } = new();
        public List<CustomDynamicField> DynamicFields { get; set; } = new();
        public TransactionEvaluationDetail? EvaluationDetail { get; set; }

        public Dictionary<string, List<string>> GroupedBreakdowns { get; set; } = new();

        public decimal PricePerDay { get; set; }
        public string StripePaymentLink { get; set; } = "";
        public int NumberOfNights => (Reservation.EndDate - Reservation.StartDate).Days;

        [BindProperty(SupportsGet = true)]
        public string? Origin { get; set; }
        [BindProperty(SupportsGet = true)]
        public string? ReturnUrl { get; set; }

        public bool IsReservationCancelled =>
            Reservation.ReservationStatus == SD.CancelledReservation;

        private bool IsPayment(Transaction t) =>
            t.Fee?.Name?.Equals("payment", StringComparison.OrdinalIgnoreCase) == true;

        private bool IsCancellation(Transaction t) =>
            t.Fee?.DisplayLabel?.ToLower().Contains("cancel") == true;

        public decimal BaseCharges => AllTransactions
            .Where(t => t.Amount > 0 && t.Fee?.DisplayLabel?.ToLower().Contains("base") == true)
            .Sum(t => t.Amount);

        public decimal OtherFees => AllTransactions
            .Where(t => t.Amount > 0 &&
                        !IsCancellation(t) &&
                        !(t.Fee?.DisplayLabel?.ToLower().Contains("base") ?? false))
            .Sum(t => t.Amount);

        public decimal TotalRefunded => AllTransactions
            .Where(t => IsPayment(t) && t.Amount < 0 && t.IsPaid)
            .Sum(t => t.Amount);

        public decimal CancellationFee => AllTransactions
            .Where(t => t.Amount > 0 && IsCancellation(t))
            .Sum(t => t.Amount);

        public decimal FinalBalance =>
            AllTransactions
                .Where(t => !t.IsPaid)
                .Sum(t => t.Amount);

        public decimal AmountPaid => AllTransactions
            .Where(t => IsPayment(t) && t.Amount > 0 && t.IsPaid)
            .Sum(t => t.Amount);

        public ReservationDetailsModel(
            UnitOfWork unitOfWork,
            TransactionService transactionService,
            ApplicationDbContext dbContext,
            IConfiguration config)
        {
            _unitOfWork = unitOfWork;
            _transactionService = transactionService;
            _dbContext = dbContext;
            _config = config;

            _transactionTypes = _unitOfWork.Fee.GetAll();
        }

        public class TransactionGroupDto
        {
            public string Label { get; set; } = "";
            public List<Transaction> Transactions { get; set; } = new();
            public string Status { get; set; } = "";
            public decimal NetAmount => Transactions.Sum(t => t.Amount);
        }

        public async Task<IActionResult> OnGetAsync(int id, string? origin, string? returnUrl)
        {
            Origin = origin;
            ReturnUrl = returnUrl;

            Reservation = await _dbContext.Reservation
                .Include(r => r.Site).ThenInclude(s => s.SiteType)
                .Include(r => r.UserAccount)
                .FirstOrDefaultAsync(r => r.ReservationId == id);

            if (Reservation == null)
                return NotFound();

            if (Reservation.Site?.SiteTypeId is int siteTypeId)
            {
                var reservationStart = Reservation.StartDate;
                var price = await _unitOfWork.Price.GetFirstOrDefaultAsync(
                    p => p.SiteTypeId == siteTypeId &&
                         p.StartDate <= reservationStart &&
                         (p.EndDate == null || p.EndDate >= reservationStart)
                );

                PricePerDay = price?.PricePerDay ?? 0;
            }

            var summaryDtos = await _transactionService.ApplyTriggeredTransactionsAsync(
                Reservation, PricePerDay, NumberOfNights);

            var existingTriggeredTypes = _unitOfWork.Transaction
                .GetAll(t => t.ReservationId == Reservation.ReservationId && t.TriggerType == TriggerType.Automatic)
                .Select(t => t.FeeId)
                .ToHashSet();

            foreach (var summary in summaryDtos)
            {
                if (!existingTriggeredTypes.Contains(summary.FeeId))
                {
                    var transactionType = _transactionTypes.FirstOrDefault(t => t.FeeId == summary.FeeId);

                    var newTxn = new Transaction
                    {
                        ReservationId = Reservation.ReservationId,
                        FeeId = summary.FeeId,
                        Amount = summary.Amount,
                        TransactionDateTime = DateTime.UtcNow,
                        TriggerType = TriggerType.Automatic,
                        CalculationType = summary.CalculationType,
                        TriggerRuleSnapshotJson = transactionType?.TriggerRuleJson ?? "",
                        Fee = transactionType
                    };

                    _unitOfWork.Transaction.Add(newTxn);
                }
            }

            await _unitOfWork.CommitAsync();

            AllTransactions = _unitOfWork.Transaction
                .GetAll(t => t.ReservationId == Reservation.ReservationId, includes: "Fee")
                .ToList();

            bool hasCancellationTxn = AllTransactions.Any(t => IsCancellation(t));

            if (IsReservationCancelled && !hasCancellationTxn)
            {
                var cancellationType = await _unitOfWork.Fee
                    .GetFirstOrDefaultAsync(t => t.DisplayLabel.ToLower().Contains("cancel"));

                if (cancellationType != null)
                {
                    var cancellationTxn = new Transaction
                    {
                        ReservationId = Reservation.ReservationId,
                        FeeId = cancellationType.FeeId,
                        Amount = cancellationType.StaticAmount ?? 27.00m,
                        Description = "Cancellation Fee",
                        TransactionDateTime = DateTime.UtcNow,
                        IsPaid = true,
                        TriggerType = TriggerType.Automatic
                    };

                    _unitOfWork.Transaction.Add(cancellationTxn);
                    await _unitOfWork.CommitAsync();

                    AllTransactions.Add(cancellationTxn);
                }
            }

            DynamicFields = _unitOfWork.CustomDynamicField
                .GetAll(f => !f.IsDeleted && f.IsEnabled)
                .ToList();

            var baseUrl = _config["AppSettings:BaseUrl"];
            StripePaymentLink = FinalBalance > 0
                ? $"{baseUrl}/Client/Reservations/Payments?reservationId={Reservation.ReservationId}&amountDue={FinalBalance}"
                : "";

            _transactionService.AuditService.GetEvaluations(Reservation.ReservationId).Clear();
            _transactionService.ApplyTriggeredTransactionsAsync(Reservation, PricePerDay, NumberOfNights);

            var breakdownService = new TriggerBreakdownService(DynamicFields);

            foreach (var txn in AllTransactions)
            {
                TransactionStatuses[txn.TransactionId] = GetTransactionStatus(txn);

                if (txn.Fee?.Name == "base" || txn.Fee?.DisplayLabel?.ToLower().Contains("base") == true)
                {
                    var nights = NumberOfNights;
                    var rate = PricePerDay;
                    var total = nights * rate;

                    TransactionBreakdowns[txn.TransactionId] = $@"
                        <strong>Calculation:</strong><br/>
                        {nights} nights × {rate.ToString("C")} = <strong>{total.ToString("C")}</strong>
                    ";
                }
                else if (!string.IsNullOrWhiteSpace(txn.TriggerRuleSnapshotJson))
                {
                    try
                    {
                        var ruleText = breakdownService.ToHumanReadable(txn.TriggerRuleSnapshotJson);
                        var eval = _transactionService.AuditService.GetEvaluation(Reservation.ReservationId, txn.FeeId);

                        if (eval != null)
                        {
                            var breakdown = eval.GetBreakdown();
                            TransactionBreakdowns[txn.TransactionId] = $@"
                                {ruleText}<br/>
                                <strong>Calculation:</strong> {breakdown}
                            ";
                        }
                        else
                        {
                            TransactionBreakdowns[txn.TransactionId] = "<em class='text-danger'>No evaluation available.</em>";
                        }
                    }
                    catch
                    {
                        TransactionBreakdowns[txn.TransactionId] = "<em class='text-danger'>Error generating breakdown.</em>";
                    }
                }
            }

            GroupedTransactions = AllTransactions
                .GroupBy(t => GetTransactionLabel(t))
                .Select(g => new TransactionGroupDto
                {
                    Label = g.Key,
                    Transactions = g.ToList(),
                    Status = GetGroupStatus(g.ToList())
                })
                .OrderBy(g => g.Label switch
                {
                    "Base Charges" => 1,
                    "Other" => 2,
                    "Refund" => 3,
                    "Cancellation Fee" => 4,
                    _ => 99
                })
                .ToList();

            return Page();
        }

        private string GetTransactionLabel(Transaction txn)
        {
            var label = txn.Fee?.DisplayLabel?.ToLower() ?? "";
            var name = txn.Fee?.Name?.ToLower() ?? "";

            if (label.Contains("base")) return "Base Charges";
            if (label.Contains("cancel")) return "Cancellation Fee";
            if (txn.Amount < 0 && name == "payment") return "Refund";
            if (name == "payment") return "Payment";
            return "Fees";
        }

        private string GetTransactionStatus(Transaction txn)
        {
            if (txn.Amount < 0)
                return "Refunded";

            if (IsCancellation(txn) && IsReservationCancelled)
                return "Paid";

            return txn.IsPaid ? "Paid" : "Unpaid";
        }

        private string GetGroupStatus(List<Transaction> txns)
        {
            if (txns.All(t => t.IsPaid)) return "Paid";
            if (txns.Any(t => t.IsPaid)) return "Partially Paid";
            if (txns.Sum(t => t.Amount) < 0) return "Refunded";
            return "Unpaid";
        }

        public string BadgeClassFromStatus(string status) => status switch
        {
            "Paid" => "bg-success",
            "Partially Paid" => "bg-warning",
            "Refunded" => "bg-info",
            "Unpaid" => "bg-danger",
            _ => ""
        };
    }
}
