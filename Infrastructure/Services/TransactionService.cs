using ApplicationCore.Dtos;
using ApplicationCore.Enums;
using ApplicationCore.Models;
using Infrastructure.Data;
using Infrastructure.Utilities;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Threading.Tasks;

namespace Infrastructure.Services
{
    public class TransactionService
    {
        private readonly IEnumerable<Fee> _Fees;
        private readonly TransactionAuditService _auditService;
        private readonly UnitOfWork _unitOfWork;
        private readonly ILogger<TransactionService> _logger;
        public TransactionAuditService AuditService => _auditService;

        public TransactionService(
            IEnumerable<Fee> Fees,
            TransactionAuditService auditService,
            UnitOfWork unitOfWork,
            ILogger<TransactionService> logger)
        {
            _Fees = Fees;
            _auditService = auditService;
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<List<TransactionSummaryDto>> ApplyTriggeredTransactionsAsync(
            Reservation reservation,
            decimal baseAmount,
            int numberOfDays)
        {
            var results = new List<TransactionSummaryDto>();

            foreach (var fee in _Fees
                .Where(t =>
                    t.TriggerType == TriggerType.Automatic &&
                    t.IsEnabled &&
                    !(t.Name?.ToLower().Contains("cancel") == true || t.DisplayLabel?.ToLower().Contains("cancel") == true)))
            {
                int units = 0;

                if (!string.IsNullOrWhiteSpace(fee.TriggerRuleJson))
                {
                    // First check if rule matches at all
                    if (!TriggerRuleEvaluator.Evaluate(fee.TriggerRuleJson!, reservation))
                    {
                        Console.WriteLine($"[Fee Skip] '{fee.Name}' - rule did not match");
                        continue;
                    }

                    units = TriggerRuleEvaluator.EvaluateUnits(fee.TriggerRuleJson!, reservation);
                    Console.WriteLine($"[Fee Eval] '{fee.Name}' - units: {units}");
                }
                else if (fee.CustomDynamicFieldId.HasValue && reservation.DynamicData != null)
                {
                    var customField = _unitOfWork.CustomDynamicField.Get(cd => cd.Id == fee.CustomDynamicFieldId.Value);
                    if (customField != null)
                    {
                        var field = reservation.DynamicData.FirstOrDefault(f => f.Key.Equals(customField.FieldName, StringComparison.OrdinalIgnoreCase));
                        if (!string.IsNullOrWhiteSpace(field.Key))
                        {
                            var value = field.Value?.ToString();
                            if (decimal.TryParse(value, out var num))
                                units = num > 0 ? (int)Math.Ceiling(num) : 0;
                            else if (!string.IsNullOrWhiteSpace(value))
                                units = 1;
                        }
                    }
                }

                if (units == 0)
                {
                    Console.WriteLine($"[Fee Skip] '{fee.Name}' - 0 units");
                    continue;
                }

                // Get calculation parameters from the Fee entity itself, NOT from TriggerRuleJson
                var calcType = fee.CalculationType ?? CalculationType.StaticAmount;
                var staticAmt = fee.StaticAmount ?? 0m;
                var percentage = fee.Percentage ?? 0m;

                // Determine if this is a per-night fee based on CalculationType
                bool isPerNight = calcType == CalculationType.DailyRate ||
                                  calcType == CalculationType.PerUnit;

                decimal total = calcType switch
                {
                    CalculationType.StaticAmount => staticAmt,
                    CalculationType.PercentOfTotal => baseAmount * (percentage / 100m),
                    CalculationType.PerUnit => staticAmt * units * numberOfDays,
                    CalculationType.DailyRate => GetDailyRateFromSite(reservation.SiteId) * numberOfDays,
                    _ => 0m
                };

                Console.WriteLine($"[Fee Calc] '{fee.Name}' - CalcType: {calcType}, StaticAmt: {staticAmt}, Units: {units}, Nights: {numberOfDays}, Total: {total}");

                _auditService.LogEvaluation(reservation.ReservationId, new TransactionEvaluationDetail
                {
                    FeeId = fee.FeeId,
                    FeeName = fee.DisplayLabel ?? fee.Name ?? "Unnamed",
                    CalculationType = calcType.ToString(),
                    Units = units,
                    PerUnit = calcType == CalculationType.PerUnit,
                    IsDailyRate = isPerNight,
                    NumberOfNights = numberOfDays,
                    Amount = total,
                    BaseAmount = baseAmount,
                    TriggerInputs = reservation.DynamicData?.ToDictionary(k => k.Key, v => (object)v.Value) ?? new()
                });

                if (total > 0)
                {
                    results.Add(new TransactionSummaryDto
                    {
                        Label = fee.DisplayLabel ?? fee.Name!,
                        Amount = Math.Round(total, 2),
                        FeeId = fee.FeeId,
                        Units = units,
                        NumberOfNights = numberOfDays,
                        PerUnitAmount = staticAmt,
                        Percentage = percentage,
                        CalculationType = calcType,
                        IsPerNight = isPerNight
                    });
                }
            }

            return await Task.FromResult(results);
        }

        public async Task<Transaction?> GetBestCancellationTransactionAsync(
            Reservation reservation, decimal baseAmount, int nights)
        {
            _logger.LogInformation($"[Cancellation] Starting cancellation evaluation for reservation {reservation.ReservationId}");

            reservation.DynamicData ??= new Dictionary<string, object>();
            if (!reservation.DynamicData.ContainsKey("HoursBefore"))
            {
                _logger.LogWarning($"[Cancellation] Reservation {reservation.ReservationId} missing 'HoursBefore'. Rule evaluation may fail.");
            }

            var candidates = _Fees.Where(t =>
                t.TriggerType == TriggerType.Automatic &&
                t.IsEnabled &&
                !string.IsNullOrWhiteSpace(t.TriggerRuleJson) &&
                (t.Name?.ToLower().Contains("cancel") == true || t.DisplayLabel?.ToLower().Contains("cancel") == true)
            ).ToList();

            Fee? bestMatch = null;
            int? bestThreshold = null;

            foreach (var type in candidates)
            {
                TriggerRule? rule = null;
                try
                {
                    rule = JsonSerializer.Deserialize<TriggerRule>(type.TriggerRuleJson!);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"[Cancellation] Failed to parse rule for {type.Name}");
                    continue;
                }

                if (rule == null || !TriggerRuleEvaluator.Evaluate(type.TriggerRuleJson!, reservation))
                    continue;

                if (!int.TryParse(rule.Value.ToString(), out int thresholdValue))
                    continue;

                if (bestThreshold == null || thresholdValue > bestThreshold)
                {
                    bestThreshold = thresholdValue;
                    bestMatch = type;
                }
            }

            if (bestMatch == null)
            {
                _logger.LogWarning("[Cancellation] No matching cancellation fee.");
                return null;
            }

            int units = TriggerRuleEvaluator.EvaluateUnits(bestMatch.TriggerRuleJson!, reservation);
            decimal amount = CalculateTriggeredAmount(bestMatch, reservation, nights, units);

            var calcType = bestMatch.CalculationType ?? CalculationType.StaticAmount;

            _auditService.LogEvaluation(reservation.ReservationId, new TransactionEvaluationDetail
            {
                FeeId = bestMatch.FeeId,
                FeeName = bestMatch.DisplayLabel ?? bestMatch.Name ?? "Unnamed",
                CalculationType = calcType.ToString(),
                Units = units,
                PerUnit = calcType == CalculationType.PerUnit,
                IsDailyRate = calcType == CalculationType.DailyRate || calcType == CalculationType.PerUnit,
                NumberOfNights = nights,
                Amount = amount,
                BaseAmount = baseAmount,
                TriggerInputs = reservation.DynamicData?.ToDictionary(k => k.Key, v => (object)v.Value) ?? new()
            });

            var tx = new Transaction
            {
                ReservationId = reservation.ReservationId,
                FeeId = bestMatch.FeeId,
                Amount = amount,
                PaymentMethod = null,
                Description = $"Cancellation fee applied: {bestMatch.Name}",
                TransactionDateTime = DateTime.UtcNow,
                TriggerType = TriggerType.Automatic,
                TriggerRuleSnapshotJson = bestMatch.TriggerRuleJson,
                CalculationType = bestMatch.CalculationType ?? CalculationType.StaticAmount,
                IsPaid = false
            };

            return tx;
        }

        // Made to check the actual amount that was paid. If we re-calculated it every time, there's a good chance it'd be inaccurate.
        public decimal CalculateCurrentCost(Reservation reservation)
        {
            if (reservation == null)
            {
                return 0; // This should likely return an error instead, as it means the reservation doesn't exist at all
            }

            var nights = (reservation.EndDate.Date - reservation.StartDate.Date).Days;
            var transactions = _unitOfWork.Transaction.GetAll(t => t.ReservationId == reservation.ReservationId && (t.FeeId == 1 || t.FeeId == 5)).ToList(); // Get base reservation costs, plus any previous refunds or other changes. TODO: this will include refunds that are issued for other reasons! We need to think of a better way to do this in general
            //old line: return transactions.Where(t => !t.PreviouslyRefunded).Sum(t => t.Amount); // TODO: check that older Base Reservation transactions are being fully refunded, so that this actually works
            //it actually needs to include previously refunded transactions!!! the refunded transactions will cancel them out, giving the correct current transaction values
            //this is just the base reservations...
            return transactions.Sum(t => t.Amount); 
        }

        public decimal CalculateReservationCost(int siteId, DateTime start, DateTime end)
        {
            var site = _unitOfWork.Site.Get(s => s.SiteId == siteId);
            if (site == null || !site.SiteTypeId.HasValue)
            {
                return 0;
            }

            var nights = (end.Date - start.Date).Days;

            // Get SiteType's related Price objects
            var prices = _unitOfWork.Price.GetAll(
                p => p.SiteTypeId == site.SiteTypeId &&
                (!(end < p.StartDate) && (p.EndDate == null || !(start > p.EndDate)))
            ).OrderBy(p => p.StartDate);

            var adjustedPrices = prices.Select(p => new {
                Start = p.StartDate < start ? start : p.StartDate,
                End = (p.EndDate == null || p.EndDate > end) ? end : p.EndDate.Value,
                p.PricePerDay
            }).ToList();

            // Format Price objects so that they only apply to the reservation date range, for easier display in Summary.cshtml
            //this overwrites the actual prices in the database, which is bad
            //foreach (var price in pricesCopy)
            //{
            //    if (price.StartDate < start) price.StartDate = start;
            //    if (price.EndDate == null || price.EndDate > end) price.EndDate = end;
            //}

            // Calculate the BaseAmount by iterating day-by-day
            decimal baseAmount = 0;
            for (int i = 0; i < nights; i++)
            {
                var date = start.AddDays(i);
                var priceForDate = adjustedPrices
                    .Where(p => p.Start <= date && p.End >= date)
                    .Select(p => p.PricePerDay)
                    .FirstOrDefault();
                baseAmount += priceForDate;
            }


            return baseAmount;
        }

        //overloaded to accept a reservation object
        public decimal CalculateReservationCost(Reservation reservation)
        {
            var site = _unitOfWork.Site.Get(s => s.SiteId == reservation.SiteId);
            if (site == null || !site.SiteTypeId.HasValue)
            {
                return 0;
            }

            var nights = (reservation.EndDate - reservation.StartDate).Days;

            // Get SiteType's related Price objects
            var prices = _unitOfWork.Price.GetAll(
                p => p.SiteTypeId == site.SiteTypeId &&
                (!(reservation.EndDate < p.StartDate) && (p.EndDate == null || !(reservation.StartDate > p.EndDate)))
            ).OrderBy(p => p.StartDate);


            var pricesCopy = prices.ToList(); //making a copy because otherwise, it adjusts the actual prices in the database

            var adjustedPrices = prices.Select(p => new {
                Start = p.StartDate < reservation.StartDate ? reservation.StartDate : p.StartDate,
                End = (p.EndDate == null || p.EndDate > reservation.EndDate) ? reservation.EndDate : p.EndDate.Value,
                p.PricePerDay
            }).ToList();


            // Calculate the BaseAmount by iterating day-by-day
            decimal baseAmount = 0;
            for (int i = 0; i < nights; i++)
            {
                var date = reservation.StartDate.AddDays(i);
                var priceForDate = adjustedPrices
                    .Where(p => p.Start <= date && p.End >= date)
                    .Select(p => p.PricePerDay)
                    .FirstOrDefault();
                baseAmount += priceForDate;
            }

            return baseAmount;
        }

        public decimal CalculateTriggeredAmount(Fee fee, Reservation reservation, int nights, int units)
        {
            var calcType = fee.CalculationType ?? CalculationType.StaticAmount;
            var staticAmt = fee.StaticAmount ?? 0m;
            var percentage = fee.Percentage ?? 0m;

            decimal baseReservationAmount = GetDailyRateFromSite(reservation.SiteId) * nights;

            return calcType switch
            {
                CalculationType.StaticAmount => staticAmt,
                CalculationType.PercentOfTotal => baseReservationAmount * (percentage / 100m),
                CalculationType.PerUnit => staticAmt * units * nights,
                CalculationType.DailyRate => CalculateReservationCost(reservation.SiteId, reservation.StartDate, reservation.EndDate),
                _ => 0m
            };
        }

        public decimal GetDailyRateFromSite(int siteId)
        {
            var site = _unitOfWork.Site.Get(s => s.SiteId == siteId, includes: "SiteType");
            if (site?.SiteTypeId == null) return 0;

            var today = DateTime.UtcNow.Date;
            var price = _unitOfWork.Price.GetAll(p =>
                p.SiteTypeId == site.SiteTypeId &&
                p.StartDate <= today &&
                (p.EndDate == null || p.EndDate >= today))
                .OrderByDescending(p => p.StartDate)
                .FirstOrDefault();

            return (decimal)(price?.PricePerDay ?? 0);
        }

        public class TransactionAuditService
        {
            private readonly Dictionary<int, List<TransactionEvaluationDetail>> _logsByReservation = new();

            public void LogEvaluation(int reservationId, TransactionEvaluationDetail detail)
            {
                if (!_logsByReservation.ContainsKey(reservationId))
                    _logsByReservation[reservationId] = new List<TransactionEvaluationDetail>();

                _logsByReservation[reservationId].Add(detail);
            }

            public TransactionEvaluationDetail? GetEvaluation(int reservationId, int FeeId)
            {
                return _logsByReservation.TryGetValue(reservationId, out var list)
                    ? list.LastOrDefault(e => e.FeeId == FeeId)
                    : null;
            }

            public List<TransactionEvaluationDetail> GetEvaluations(int reservationId)
            {
                return _logsByReservation.TryGetValue(reservationId, out var list) ? list : new();
            }
        }
    }
}
