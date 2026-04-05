using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ApplicationCore.Dtos;
using ApplicationCore.Models;
using Infrastructure.Data;
using Infrastructure.Services;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Antiforgery;
using Stripe;
using Infrastructure.Utilities;

namespace RVPark.Pages.Client.Reservations
{
    public class PaymentModel : PageModel
    {
        private readonly UnitOfWork _unitOfWork;
        private readonly IConfiguration _config;
        private readonly IEmailSender _emailSender;
        private readonly TransactionService _transactionService;

        public Reservation Reservation { get; set; } = new();
        public List<Transaction> UnpaidTransactions { get; set; } = new();
        public List<TransactionSummaryDto> TransactionSummaries { get; set; } = new();

        // For the table footer initial render; JS updates after selection
        public decimal SelectedTotal { get; set; } = 0m;
        public string PublishableKey { get; set; } = string.Empty;

        public PaymentModel(
            UnitOfWork unitOfWork,
            IConfiguration config,
            IEmailSender emailSender,
            TransactionService transactionService)
        {
            _unitOfWork = unitOfWork;
            _config = config;
            _emailSender = emailSender;
            _transactionService = transactionService;
        }

        // Keep amountDue & transactionId optional so old links still work.
        public async Task<IActionResult> OnGetAsync(int reservationId, decimal? amountDue = null, int? transactionId = null)
        {
            // Load reservation
            Reservation = await _unitOfWork.Reservation.GetFirstOrDefaultAsync(r => r.ReservationId == reservationId);
            if (Reservation == null) return NotFound("Reservation not found.");

            Reservation.UserAccount = await _unitOfWork.UserAccount.GetFirstOrDefaultAsync(u => u.Id == Reservation.UserId);
            Reservation.Site = await _unitOfWork.Site.GetFirstOrDefaultAsync(s => s.SiteId == Reservation.SiteId);

            // Load all unpaid transactions for this reservation
            UnpaidTransactions = _unitOfWork.Transaction
                .GetAll(t => t.ReservationId == reservationId && !t.IsPaid && !t.PreviouslyRefunded, includes: "Fee")
                .OrderBy(t => t.TransactionDateTime)
                .ToList();

            // Legacy: specific transaction link
            if (transactionId.HasValue)
            {
                var tx = UnpaidTransactions.FirstOrDefault(t => t.TransactionId == transactionId.Value);
                if (tx != null)
                {
                    SelectedTotal = tx.Amount;
                }
            }
            // Legacy: amountDue link
            else if (amountDue.HasValue && amountDue.Value > 0m)
            {
                var existing = UnpaidTransactions.FirstOrDefault(t => t.Amount == amountDue.Value);
                if (existing == null)
                {
                    var manualType = await _unitOfWork.Fee.GetFirstOrDefaultAsync(
                        t => t.TriggerType == ApplicationCore.Enums.TriggerType.Manual &&
                             t.Name.ToLower().Contains("manual"));

                    if (manualType != null)
                    {
                        var tx = new Transaction
                        {
                            ReservationId = reservationId,
                            FeeId = manualType.FeeId,
                            Amount = amountDue.Value,
                            Description = "Manual Payment (legacy link)",
                            TransactionDateTime = DateTime.UtcNow,
                            TriggerType = ApplicationCore.Enums.TriggerType.Manual,
                            CalculationType = ApplicationCore.Enums.CalculationType.StaticAmount
                        };
                        await _unitOfWork.Transaction.AddAsync(tx);
                        await _unitOfWork.CommitAsync();
                        UnpaidTransactions.Add(tx);
                        SelectedTotal = tx.Amount;
                    }
                }
                else
                {
                    SelectedTotal = existing.Amount;
                }
            }

            var baseAmount = _transactionService.CalculateReservationCost(Reservation.SiteId, Reservation.StartDate, Reservation.EndDate);
            var numberOfDays = (Reservation.EndDate - Reservation.StartDate).Days;
            TransactionSummaries = await _transactionService.ApplyTriggeredTransactionsAsync(Reservation, baseAmount, numberOfDays);

            // Stripe keys
            StripeConfiguration.ApiKey = _config["Stripe:SecretKey"];
            PublishableKey = _config["Stripe:PublishableKey"];

            return Page();
        }

        public class CreateIntentResponse
        {
            public string ClientSecret { get; set; } = "";
            public long Total { get; set; } // cents
        }

        [IgnoreAntiforgeryToken]
        public async Task<JsonResult> OnPostCreateIntentAsync([FromBody] int[] transactionIds)
        {
            try
            {
                if (transactionIds == null || transactionIds.Length == 0)
                    return new JsonResult(new { error = "No transactions selected." }) { StatusCode = 400 };

                var txns = _unitOfWork.Transaction
                    .GetAll(t => transactionIds.Contains(t.TransactionId) && !t.IsPaid && !t.PreviouslyRefunded)
                    .ToList();

                if (txns.Count == 0)
                    return new JsonResult(new { error = "No valid unpaid transactions." }) { StatusCode = 400 };

                var reservationId = txns.First().ReservationId;
                if (txns.Any(t => t.ReservationId != reservationId))
                    return new JsonResult(new { error = "Mixed reservations not allowed." }) { StatusCode = 400 };

                var amountInCents = (long)Math.Round(txns.Sum(t => t.Amount) * 100m, MidpointRounding.AwayFromZero);

                StripeConfiguration.ApiKey = _config["Stripe:SecretKey"];
                var svc = new PaymentIntentService();
                var intent = await svc.CreateAsync(new PaymentIntentCreateOptions
                {
                    Amount = amountInCents,
                    Currency = "usd",
                    Metadata = new Dictionary<string, string>
                {
                    { "ReservationId", reservationId.ToString() },
                    { "TransactionIds", string.Join(",", txns.Select(t => t.TransactionId)) }
                }
                });

                return new JsonResult(new { clientSecret = intent.ClientSecret, total = amountInCents }) { StatusCode = 200 };
            }
            catch (Exception ex)
            {
                return new JsonResult(new { error = "Server error creating PaymentIntent.", detail = ex.Message }) { StatusCode = 500 };
            }
        }

        public async Task<IActionResult> OnPostManualPaymentAsync(int transactionId, string? note)
        {
            var transaction = await _unitOfWork.Transaction.GetFirstOrDefaultAsync(t => t.TransactionId == transactionId);
            if (transaction == null) return NotFound();

            transaction.IsPaid = true;
            transaction.PaymentMethod = SD.CreditCardPayment;
            transaction.Description += string.IsNullOrWhiteSpace(note)
                ? " (Marked paid manually)"
                : $" (Manual Payment: {note})";
            transaction.TransactionDateTime = DateTime.UtcNow;

            _unitOfWork.Transaction.Update(transaction);
            await _unitOfWork.CommitAsync();

            return RedirectToPage("/Client/Reservations/Receipt", new { reservationId = transaction.ReservationId });
        }
    }
}
