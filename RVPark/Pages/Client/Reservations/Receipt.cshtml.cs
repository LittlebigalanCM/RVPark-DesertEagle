using ApplicationCore.Dtos;
using ApplicationCore.Enums;
using ApplicationCore.Models;
using Humanizer.Bytes;
using Infrastructure.Data;
using Infrastructure.Services;
using Infrastructure.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using PdfSharpCore.Pdf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using static Infrastructure.Services.TransactionService;

namespace RVPark.Pages.Client.Reservations
{
    public class ReceiptModel : PageModel
    {
        private readonly UnitOfWork _unitOfWork;
        private readonly ILogger<TransactionService> _logger;
        private readonly TransactionService _transactionService;
        private readonly IConfiguration _config;

        public Dictionary<int, TransactionEvaluationDetail> EvaluationDetails { get; set; } = new();

        public ReceiptModel(UnitOfWork unitOfWork, ILogger<TransactionService> logger, TransactionService transactionService, IConfiguration config)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
            _transactionService = transactionService;
            _config = config;
        }

        public Reservation Reservation { get; set; } = new();
        public List<Transaction> Transactions { get; set; } = new();

        public decimal TotalPaid => Transactions.Where(t => t.IsPaid && t.Amount > 0).Sum(t => t.Amount);
        public decimal TotalRefunded => Transactions.Where(t => t.Amount < 0).Sum(t => t.Amount);
        public decimal BalanceDue { get; set; }

        [BindProperty(SupportsGet = true)]
        public int ReservationId { get; set; }

        public int? PaymentTransactionId { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Origin { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? ReturnUrl { get; set; }

        public string StripePaymentLink { get; set; } = "";

        public async Task<IActionResult> OnGetAsync(string? origin, string? returnUrl)
        {
            Origin = origin;
            ReturnUrl = returnUrl;

            if (ReservationId == 0 && Request.Query.ContainsKey("reservationId"))
            {
                if (int.TryParse(Request.Query["reservationId"], out int resId))
                    ReservationId = resId;
                else
                    return BadRequest("Invalid reservation ID.");
            }

            if (ReservationId == 0)
                return BadRequest("Missing reservation ID.");

            if (Request.Query.ContainsKey("transactionId") &&
                int.TryParse(Request.Query["transactionId"], out int txId))
            {
                PaymentTransactionId = txId;
            }

            await LoadData(ReservationId, PaymentTransactionId);
            return Page();
        }

        public async Task<IActionResult> OnGetDownloadReceiptAsync([FromQuery] int reservationId, [FromQuery] int? transactionId)
        {
            if (reservationId <= 0)
            {
                if (Request.Query.TryGetValue("reservationId", out var val) && int.TryParse(val, out int fallbackId))
                    reservationId = fallbackId;
                else
                    return BadRequest("Invalid reservation ID.");
            }

            await LoadData(reservationId, transactionId);

            var nights = Math.Max((int)(Reservation.EndDate.Date - Reservation.StartDate.Date).TotalDays, 0);
            var summaryList = new List<TransactionSummaryDto>();

            foreach (var tx in Transactions.OrderBy(t => t.TransactionDateTime))
            {
                var label = !string.IsNullOrWhiteSpace(tx.Description)
                    ? tx.Description
                    : (tx.Fee?.DisplayLabel ?? tx.Fee?.Name ?? "Transaction");

                if (tx.Amount >= 0)
                {
                    var breakdown = GetBreakdown(tx, nights, Reservation.DynamicData ?? new Dictionary<string, object>());
                    if (!string.IsNullOrWhiteSpace(breakdown) && !label.Contains(breakdown, StringComparison.OrdinalIgnoreCase))
                        label += "\n- " + breakdown;
                }

                var eval = EvaluationDetails.TryGetValue(tx.FeeId, out var e) ? e : null;

                summaryList.Add(new TransactionSummaryDto
                {
                    Label = label,
                    Amount = tx.Amount,
                    FeeId = tx.FeeId,
                    Status = tx.IsPaid ? "Paid" : "Unpaid",
                    TransactionDateTime = tx.TransactionDateTime,
                    CalculationType = tx.CalculationType,
                    Units = eval?.Units ?? 0,
                    PerUnitAmount = eval?.ComputedPerUnitAmount ?? 0
                });
            }

            var stream = PdfExporter.ExportReceiptToPdf(Reservation, summaryList);
            return File(stream.ToArray(), "application/pdf", $"receipt-{reservationId}.pdf");
        }

        private async Task LoadData(int reservationId, int? transactionId = null)
        {
            Reservation = await _unitOfWork.Reservation.GetFirstOrDefaultAsync(r => r.ReservationId == reservationId)
                          ?? throw new InvalidOperationException($"Reservation ID {reservationId} not found.");
            ReservationId = Reservation.ReservationId;

            Reservation.UserAccount = await _unitOfWork.UserAccount.GetFirstOrDefaultAsync(u => u.Id == Reservation.UserId);
            Reservation.Site = await _unitOfWork.Site.GetFirstOrDefaultAsync(s => s.SiteId == Reservation.SiteId);

            Transactions = _unitOfWork.Transaction
                .GetAll(t => t.ReservationId == reservationId)
                .OrderByDescending(t => t.TransactionDateTime)
                .ToList();

            if (transactionId.HasValue && !Transactions.Any(t => t.TransactionId == transactionId))
            {
                var extra = await _unitOfWork.Transaction.GetFirstOrDefaultAsync(t => t.TransactionId == transactionId.Value);
                if (extra != null)
                {
                    Transactions.Add(extra);
                    Transactions = Transactions.OrderByDescending(t => t.TransactionDateTime).ToList();
                }
            }

            var nights = (Reservation.EndDate.Date - Reservation.StartDate.Date).Days;
            var auditService = new TransactionAuditService();
            var triggeredTypes = _unitOfWork.Fee
                .GetAll(t => t.TriggerType == TriggerType.Automatic)
                .ToList();

            var txService = new TransactionService(triggeredTypes, auditService, _unitOfWork, _logger);

   
            var dynamicFields = _unitOfWork.CustomDynamicField
                .GetAll(f => !f.IsDeleted && f.IsEnabled)
                .ToList();

            if (Reservation.DynamicData == null)
                Reservation.DynamicData = new Dictionary<string, object>();

            foreach (var field in dynamicFields)
            {
                if (!Reservation.DynamicData.ContainsKey(field.FieldName))
                {
                    object value = field.DefaultValue.HasValue ? field.DefaultValue.Value : 0;
                    Reservation.DynamicData[field.FieldName] = value;
                }
            }

            // TODO: At some point, check that this site and site type actually exist
            var site = _unitOfWork.Site.GetAll(s => s.SiteId == Reservation.SiteId).FirstOrDefault();
            var siteType = _unitOfWork.SiteType.GetAll(s => s.SiteTypeId == site.SiteTypeId).FirstOrDefault();

            // Get SiteType's related Price objects
            var prices = _unitOfWork.Price.GetAll(
                p => p.SiteTypeId == site.SiteTypeId &&
                (!(Reservation.EndDate < p.StartDate) && (p.EndDate == null || !(Reservation.StartDate > p.EndDate)))
            ).OrderBy(p => p.StartDate);

            // Format Price objects so that they only apply to the reservation date range, for easier display in Summary.cshtml
            foreach (var price in prices)
            {
                if (price.StartDate < Reservation.StartDate) price.StartDate = Reservation.StartDate;
                if (price.EndDate == null || price.EndDate > Reservation.EndDate) price.EndDate = Reservation.EndDate;
            }

            // Calculate the BaseAmount by iterating day-by-day
            decimal baseAmount = 0;
            for (int i = 0; i < nights; i++)
            {
                var date = Reservation.StartDate.AddDays(i);
                var priceForDate = prices
                    .Where(p => p.StartDate <= date && p.EndDate >= date)
                    .Select(p => p.PricePerDay)
                    .FirstOrDefault();
                baseAmount += priceForDate;
            }

            // This code was causing odd duplicate transactions to appear when the baseAmount paid previously didn't match the baseAmount that currently
            // exists. (The baseAmount can change if the reservation is edited.) Removing it fixed the bug, but I couldn't figure out why the heck this
            // was here in the first place? I really tried to think of a reason why the previous team would've needed this, but I just couldn't. Just in
            // case that reason reveals itself in a spectacularly devastating way, I'll leave the code here, commented out.
            // - Matt
            //var realBase = Transactions.FirstOrDefault(t =>
            //    t.Description?.ToLower().Contains("base reservation") == true &&
            //    t.Amount == baseAmount);

            //if (realBase != null)
            //{
            //    Transactions.Remove(realBase);
            //}

            //var baseTransaction = new Transaction
            //{
            //    TransactionId = -1,
            //    ReservationId = Reservation.ReservationId,
            //    Amount = baseAmount,
            //    IsPaid = true,
            //    Fee = new Fee { Name = "Base Reservation" },
            //    Description = "Base Reservation",
            //    TransactionDateTime = Reservation.StartDate.AddMinutes(-1),
            //    CalculationType = CalculationType.DailyRate
            //};
            //Transactions.Insert(0, baseTransaction);

            auditService.LogEvaluation(Reservation.ReservationId, new TransactionEvaluationDetail
            {
                FeeId = -1,
                FeeName = "Base Reservation Fee",
                CalculationType = CalculationType.DailyRate.ToString(),
                Units = 1,
                PerUnit = false,
                IsDailyRate = true,
                NumberOfNights = nights,
                //DailyRate = baseRate,
                DailyRate = baseAmount/nights,
                Amount = baseAmount
            });

            var charged = Transactions.Where(t => t.Amount > 0 && !t.PreviouslyRefunded).Sum(t => t.Amount);
            var paid = Transactions.Where(t => t.IsPaid).Sum(t => t.Amount);
            BalanceDue = charged - paid;

            txService.ApplyTriggeredTransactionsAsync(Reservation, charged, nights);

            EvaluationDetails = auditService
                .GetEvaluations(Reservation.ReservationId)
                .ToDictionary(e => e.FeeId, e => e);

            var baseUrl = _config["AppSettings:BaseUrl"]?.TrimEnd('/');
            var originParam = !string.IsNullOrEmpty(Origin) ? $"&origin={Uri.EscapeDataString(Origin)}" : "";
            var returnParam = !string.IsNullOrEmpty(ReturnUrl) ? $"&returnUrl={Uri.EscapeDataString(ReturnUrl)}" : "";

            StripePaymentLink = BalanceDue > 0
                ? $"{baseUrl}/Client/Reservations/Payments?reservationId={Reservation.ReservationId}&amountDue={BalanceDue}{originParam}{returnParam}"
                : "";
        }

        public static string GetBreakdown(Transaction tx, int nights, Dictionary<string, object> dynamicData)
        {
            if (tx?.Fee == null)
                return "Fee Manually Applied.";

            var label = tx.Fee.DisplayLabel?.ToLower() ?? tx.Fee.Name?.ToLower() ?? "";
            var ruleJson = tx.TriggerRuleSnapshotJson ?? tx.Fee.TriggerRuleJson;

            if (string.IsNullOrWhiteSpace(ruleJson))
            {
                if (label.Contains("base") && nights > 0)
                {
                    decimal nightlyRate = tx.Amount / nights;
                    return $"{nights} night(s) × {nightlyRate:C} = {tx.Amount:C}";
                }

                return "Fee Manually Applied.";
            }

            try
            {
                var rule = JsonSerializer.Deserialize<TriggerRule>(ruleJson);
                if (rule == null) return "";

                if (label.Contains("base") && nights > 0)
                {
                    decimal nightlyRate = tx.Amount / nights;
                    return $"{nights} night(s) × {nightlyRate:C} = {tx.Amount:C}";
                }

                if (label.Contains("cancel") && rule.Field?.ToLower().Contains("hoursbefore") == true)
                {
                    return $"Cancellation fee applied (triggered within {rule.Value} hours before check-in).";
                }

                int actualValue = 0;
                if (!string.IsNullOrWhiteSpace(rule.Field) && dynamicData.TryGetValue(rule.Field.ToLower(), out var val))
                {
                    int.TryParse(val?.ToString(), out actualValue);
                }

                if (rule.BaseIncluded > 0 && actualValue > rule.BaseIncluded && nights > 0)
                {
                    int over = actualValue - rule.BaseIncluded;
                    decimal perUnit = tx.Amount / (nights * over);
                    return $"{nights} night(s) × {over} extra × {perUnit:C} = {tx.Amount:C}";
                }

                if (nights > 0)
                {
                    decimal perNight = tx.Amount / nights;
                    return $"{nights} night(s) × {perNight:C} = {tx.Amount:C}";
                }

                return $"Flat fee: {tx.Amount:C}";
            }
            catch
            {
                return "Unable to calculate breakdown — invalid rule format.";
            }
        }
    }
}
