using ApplicationCore.Dtos;
using ApplicationCore.Enums;
using ApplicationCore.Models;
using Infrastructure.Data;
using Infrastructure.Services;
using Infrastructure.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using static Infrastructure.Services.TransactionService;

namespace RVPark.Pages.Client.Reservations
{
    [Authorize]
    public class SummaryModel : PageModel
    {
        private readonly UnitOfWork _unitOfWork;
        private readonly TransactionService _transactionService;
        private readonly string _stripeKey;
        private readonly TransactionAuditService _auditService;
        private readonly ILogger<TransactionService> _logger;

        public SummaryModel(UnitOfWork unitOfWork, IOptions<StripeSettings> stripeOptions, TransactionAuditService auditService, ILogger<TransactionService> logger)
        {
            _unitOfWork = unitOfWork;
            _transactionService = new TransactionService(_unitOfWork.Fee.GetAll(), auditService, _unitOfWork, logger);
            _auditService = auditService;
            _stripeKey = stripeOptions.Value.PublishableKey;
            _logger = logger;
        }

        [BindProperty]
        public Reservation Reservation { get; set; }

        [BindProperty]
        public Dictionary<string, string> DynamicDataInput { get; set; }

        public List<TransactionSummaryDto> TransactionSummaries { get; set; } = new();
        public List<TransactionEvaluationDetail> TransactionEvaluations { get; set; } = new();

        public List<CustomDynamicField> DynamicFieldDefinitions { get; set; }

        public string guestName { get; set; }
        public string guestEmail { get; set; }
        public string SiteName { get; set; }
        public string SiteType { get; set; }
        public int NumberOfDays { get; set; }
        public decimal BaseAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public string StripePublicKey => _stripeKey;
        public bool FromGuestFlow { get; set; }
        public decimal ExtraFeesTotal => TransactionSummaries?.Sum(t => (decimal)t.Amount) ?? 0;
        public bool DisablePayOnDisagree { get; set; }

        public Check Check { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public string ReturnUrl { get; set; }

        [BindProperty]
        public string PaymentMethod { get; set; }

        [BindProperty]
        public int? TransactionApprovalID { get; set; } = null;

        [BindProperty]
        public IEnumerable<ApplicationCore.Models.Price> Prices { get; set; }

        public bool RequiresPCS { get; set; }
        public bool RequiresDisability { get; set; }


        public async Task<IActionResult> OnGetAsync()
        {
            if (!User.Identity.IsAuthenticated)
                return RedirectToPage("Account/Login", new { area = "Identity" });

            if (TempData["ReservationData"] == null)
                return RedirectToPage("./Browse");

            FromGuestFlow = TempData["FromGuestFlow"]?.ToString() == "true";
            Reservation = JsonSerializer.Deserialize<Reservation>(TempData["ReservationData"].ToString())!;

            var guestUser = _unitOfWork.UserAccount.Get(u => u.Id == Reservation.UserId);
            if (guestUser != null)
            {
                guestName = guestUser.FullName;
                guestEmail = guestUser.Email;
            }

            var claimsIdentity = User.Identity as ClaimsIdentity;
            var claim = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier);
            if (claim != null && !User.IsInRole(SD.AdminRole))
            {
                var user = _unitOfWork.UserAccount.Get(u => u.Id == claim.Value);
                if (user != null)
                {
                    Reservation.UserId = user.Id;
                    guestName = user.FullName;
                    guestEmail = user.Email;
                }
            }

            SiteName = TempData["SiteName"]?.ToString();
            SiteType = TempData["SiteType"]?.ToString();
            NumberOfDays = (Reservation.EndDate - Reservation.StartDate).Days;

            Prices = GetAdjustedPrices(SiteType, Reservation.StartDate, Reservation.EndDate);

            BaseAmount = 0;
            for (int i = 0; i < NumberOfDays; i++)
            {
                var date = Reservation.StartDate.AddDays(i);
                var priceForDate = Prices
                    .Where(p => p.StartDate <= date && p.EndDate >= date)
                    .Select(p => p.PricePerDay)
                    .FirstOrDefault();
                BaseAmount += priceForDate;
            }

            RequiresPCS = TempData["RequiresPCS"].ToString().ToLower().Equals("true");
            RequiresDisability = TempData["RequiresDisability"].ToString().ToLower().Equals("true");

            TempData.Keep();

            DynamicFieldDefinitions = _unitOfWork.CustomDynamicField
                .GetAll(f => !f.IsDeleted && f.IsEnabled)
                .OrderBy(f => f.DisplayOrder)
                .ToList();

            var allowedFields = DynamicFieldDefinitions.Select(f => f.FieldName).ToHashSet();

            if (Reservation.DynamicData == null)
                Reservation.DynamicData = new Dictionary<string, object>();

            foreach (var field in DynamicFieldDefinitions)
            {
                if (!Reservation.DynamicData.ContainsKey(field.FieldName))
                {
                    object value = field.DefaultValue.HasValue ? field.DefaultValue.Value : "";
                    Reservation.DynamicData[field.FieldName] = value;
                }
            }

            DynamicDataInput = allowedFields.ToDictionary(
                key => key,
                key => Reservation.DynamicData.TryGetValue(key, out var val)
                    ? val?.ToString() ?? ""
                    : ""
            );

            NumberOfDays = (Reservation.EndDate - Reservation.StartDate).Days;

            _auditService.GetEvaluations(Reservation.ReservationId).Clear();
            TransactionSummaries = await _transactionService.ApplyTriggeredTransactionsAsync(
                Reservation,
                BaseAmount,
                NumberOfDays
            );
            TransactionEvaluations = _auditService.GetEvaluations(Reservation.ReservationId);

            TotalAmount = BaseAmount + TransactionSummaries.Sum(t => t.Amount);

            Check.CheckDateTime = DateTime.UtcNow.Date;
            PaymentMethod = "Credit";

            return Page();
        }

        public async Task<IActionResult> OnPostUpdateExtraFees()
        {
            Reservation = JsonSerializer.Deserialize<Reservation>(TempData["ReservationData"].ToString())!;
            Reservation.StartDate = DateTime.Parse(TempData["StartDate"].ToString());
            Reservation.EndDate = DateTime.Parse(TempData["EndDate"].ToString());
            Reservation.SiteId = int.Parse(TempData["SiteId"].ToString());

            TempData.Keep();

            SiteType = TempData["SiteType"]?.ToString();
            NumberOfDays = (Reservation.EndDate - Reservation.StartDate).Days;

            Prices = GetAdjustedPrices(SiteType, Reservation.StartDate, Reservation.EndDate);

            BaseAmount = 0;
            for (int i = 0; i < NumberOfDays; i++)
            {
                var date = Reservation.StartDate.AddDays(i);
                var priceForDate = Prices
                    .Where(p => p.StartDate <= date && p.EndDate >= date)
                    .Select(p => p.PricePerDay)
                    .FirstOrDefault();
                BaseAmount += priceForDate;
            }

            var fieldDefinitions = _unitOfWork
                .CustomDynamicField
                .GetAll(f => !f.IsDeleted && f.IsEnabled)
                .ToDictionary(f => f.FieldName, f => f);

            var allowedFields = fieldDefinitions.Keys.ToHashSet();
            var dynamicData = new Dictionary<string, object>();

            foreach (var kvp in Request.Form.Where(k => k.Key.StartsWith("DynamicDataInput[")))
            {
                var fieldName = kvp.Key.Replace("DynamicDataInput[", "").Replace("]", "");
                if (!allowedFields.Contains(fieldName)) continue;

                var strVal = kvp.Value.ToString()?.Trim() ?? "";

                if (fieldDefinitions.TryGetValue(fieldName, out var fieldDef))
                {
                    object parsedValue = fieldDef.FieldType switch
                    {
                        DynamicFieldType.Number => int.TryParse(strVal, out int iVal) ? iVal : 0,
                        DynamicFieldType.Checkbox => strVal.Equals("true", StringComparison.OrdinalIgnoreCase) || strVal == "on",
                        DynamicFieldType.Dropdown => strVal,
                        DynamicFieldType.TextInput => strVal,
                        _ => strVal
                    };

                    dynamicData[fieldName] = parsedValue;
                }
                else
                {
                    dynamicData[fieldName] = int.TryParse(strVal, out int iVal) ? iVal : (object)strVal;
                }
            }

            Reservation.DynamicData = dynamicData;

            _auditService.GetEvaluations(Reservation.ReservationId).Clear();
            TransactionSummaries = await _transactionService.ApplyTriggeredTransactionsAsync(
                Reservation,
                BaseAmount,
                NumberOfDays
            );

            decimal totalAmount = BaseAmount + TransactionSummaries.Sum(f => f.Amount);

            return new JsonResult(new
            {
                evaluations = TransactionSummaries.Select(t => new
                {
                    label = t.Label,
                    amount = t.Amount,
                    units = t.Units,
                    numberOfNights = t.NumberOfNights,
                    perUnitAmount = t.PerUnitAmount,
                    percentage = t.Percentage,
                    calculationType = (int)t.CalculationType,
                    isPerNight = t.IsPerNight
                }),
                baseAmount = BaseAmount.ToString("0.00"),
                totalAmount = totalAmount.ToString("0.00")
            });
        }

        public async Task<IActionResult> OnPostUpdateFieldAsync(
            [FromForm] string fieldName,
            [FromForm] string note,
            [FromForm] string noteTrigger,
            [FromForm] int? defaultValue,
            [FromForm] int? minValue,
            [FromForm] int? maxValue,
            [FromForm] string noteAgreeText,
            [FromForm] string noteDisagreeText,
            [FromForm] string agreeRedirectUrl,
            [FromForm] string disagreeRedirectUrl,
            [FromForm] bool disablePayOnDisagree)
        {
            var field = await _unitOfWork.CustomDynamicField
                .GetFirstOrDefaultAsync(f => f.FieldName == fieldName && !f.IsDeleted);

            if (field != null)
            {
                field.Note = note;
                field.NoteTrigger = noteTrigger;
                field.DefaultValue = defaultValue;
                field.MaxValue = maxValue;
                field.MinValue = minValue;
                field.NoteAgreeText = noteAgreeText;
                field.NoteDisagreeText = noteDisagreeText;
                field.AgreeRedirectUrl = string.IsNullOrWhiteSpace(agreeRedirectUrl) ? null : agreeRedirectUrl;
                field.DisagreeRedirectUrl = string.IsNullOrWhiteSpace(disagreeRedirectUrl) ? null : disagreeRedirectUrl;
                field.DisablePayOnDisagree = disablePayOnDisagree;

                _unitOfWork.CustomDynamicField.Update(field);
                await _unitOfWork.CommitAsync();
            }

            return new JsonResult(new { success = true });
        }

        public async Task<IActionResult> OnPostCheckout(string? stripeToken)
        {
            Reservation = JsonSerializer.Deserialize<Reservation>(TempData["ReservationData"].ToString())!;
            Reservation.StartDate = DateTime.Parse(TempData["StartDate"].ToString());
            Reservation.EndDate = DateTime.Parse(TempData["EndDate"].ToString());
            Reservation.SiteId = int.Parse(TempData["SiteId"].ToString());
            FromGuestFlow = TempData["FromGuestFlow"]?.ToString() == "true";

            bool adminCreated = TempData["AdminCreated"]?.ToString() == "true";
            TempData.Keep();

            if (adminCreated)
            {
                if (Reservation.UserId == null)
                {
                    TempData["ErrorMessage"] = "Please select a guest before creating a reservation.";
                    return RedirectToPage("/Admin/Reservations/Create");
                }

                var selectedUser = _unitOfWork.UserAccount.Get(u => u.Id == Reservation.UserId);
                if (selectedUser == null)
                {
                    TempData["ErrorMessage"] = "Selected user not found.";
                    return RedirectToPage("/Admin/Reservations/Create");
                }
            }
            else
            {
                var claimsIdentity = User.Identity as ClaimsIdentity;
                var claim = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier);
                if (claim != null)
                {
                    var user = _unitOfWork.UserAccount.Get(u => u.Id == claim.Value);
                    if (user != null)
                    {
                        Reservation.UserId = user.Id;
                    }
                    else
                    {
                        return RedirectToPage("./Browse");
                    }
                }
                else
                {
                    return RedirectToPage("./Browse");
                }
            }

            SiteType = TempData["SiteType"]?.ToString();
            NumberOfDays = (Reservation.EndDate - Reservation.StartDate).Days;

            Prices = GetAdjustedPrices(SiteType, Reservation.StartDate, Reservation.EndDate);

            BaseAmount = 0;
            for (int i = 0; i < NumberOfDays; i++)
            {
                var date = Reservation.StartDate.AddDays(i);
                var priceForDate = Prices
                    .Where(p => p.StartDate <= date && p.EndDate >= date)
                    .Select(p => p.PricePerDay)
                    .FirstOrDefault();
                BaseAmount += priceForDate;
            }

            var site = _unitOfWork.Site.Get(s => s.SiteId == Reservation.SiteId);
            if (site == null) return RedirectToPage("./Browse");

            var overlap = _unitOfWork.Reservation.GetAll(r =>
                r.SiteId == Reservation.SiteId &&
                r.EndDate > Reservation.StartDate &&
                r.StartDate < Reservation.EndDate &&
                r.ReservationStatus != SD.CancelledReservation &&
                r.ReservationStatus != SD.CompleteReservation);

            if (overlap.Any()) return RedirectToPage("./Browse");

            RequiresPCS = TempData["RequiresPCS"].ToString().ToLower().Equals("true");
            RequiresDisability = TempData["RequiresDisability"].ToString().ToLower().Equals("true");

            if (!RequiresPCS && !RequiresDisability)
            {
                Reservation.ReservationStatus = Reservation.StartDate > DateTime.Now
                    ? SD.UpcomingReservation
                    : (Reservation.EndDate > DateTime.Now ? SD.ActiveReservation : SD.CompleteReservation);
                Reservation.RequiresPCS = false;
                Reservation.RequiresDisability = false;
            }

            if (RequiresPCS)
            {
                Reservation.ReservationStatus = SD.PendingReservation;
                Reservation.RequiresPCS = true;
            }
            else
            {
                Reservation.RequiresPCS = false;
            }

            if (RequiresDisability)
            {
                Reservation.ReservationStatus = SD.PendingReservation;
                Reservation.RequiresDisability = true;
            }
            else
            {
                Reservation.RequiresDisability = false;
            }

            var fieldDefinitions = _unitOfWork
                .CustomDynamicField
                .GetAll(f => !f.IsDeleted && f.IsEnabled)
                .ToDictionary(f => f.FieldName, f => f);

            var allowedFields = fieldDefinitions.Keys.ToHashSet();

            Reservation.DynamicData = new Dictionary<string, object>();
            foreach (var kvp in DynamicDataInput.Where(k => allowedFields.Contains(k.Key)))
            {
                var strVal = kvp.Value?.Trim() ?? "";

                if (fieldDefinitions.TryGetValue(kvp.Key, out var fieldDef))
                {
                    object parsedValue = fieldDef.FieldType switch
                    {
                        DynamicFieldType.Number => int.TryParse(strVal, out int iVal) ? iVal : 0,
                        DynamicFieldType.Checkbox => strVal.Equals("true", StringComparison.OrdinalIgnoreCase) || strVal == "on",
                        DynamicFieldType.Dropdown => strVal,
                        DynamicFieldType.TextInput => strVal,
                        _ => strVal
                    };
                    Reservation.DynamicData[kvp.Key] = parsedValue;
                }
                else
                {
                    Reservation.DynamicData[kvp.Key] = int.TryParse(strVal, out int iVal) ? iVal : (object)strVal;
                }
            }

            _unitOfWork.Reservation.Add(Reservation);
            _unitOfWork.Commit();

            if (RequiresPCS || RequiresDisability)
            {
                int reservationID = _unitOfWork.Reservation
                    .Get(r => r.UserId == Reservation.UserId
                        && r.SiteId == Reservation.SiteId
                        && r.StartDate == Reservation.StartDate
                        && r.EndDate == Reservation.EndDate).ReservationId;

                if (RequiresPCS)
                {
                    _unitOfWork.Document.Add(new Document
                    {
                        Filepath = "",
                        FileName = "",
                        ContentType = "",
                        DocType = SD.PCSDocument,
                        IsApproved = false,
                        ReservationId = reservationID
                    });
                }

                if (RequiresDisability)
                {
                    _unitOfWork.Document.Add(new Document
                    {
                        Filepath = "",
                        FileName = "",
                        ContentType = "",
                        DocType = SD.DisabilityDocument,
                        IsApproved = false,
                        ReservationId = reservationID
                    });
                }
                _unitOfWork.Commit();
            }

            var baseTxnType = _unitOfWork.Fee.Get(t => t.Name == SD.BaseReservationCostName);
            bool paymentSucceeded = false;
            string paymentMethod = string.Empty;

            int? tempTransactionApprovalId = null;

            if (!string.IsNullOrEmpty(stripeToken))
            {
                paymentMethod = SD.CreditCardPayment;
                try
                {
                    var chargeService = new Stripe.ChargeService();
                    var charge = chargeService.Create(new Stripe.ChargeCreateOptions
                    {
                        Amount = (long)(BaseAmount * 100),
                        Currency = "usd",
                        Description = $"Reservation payment for site {Reservation.SiteId}",
                        Source = stripeToken
                    });

                    if (charge.Status == "succeeded")
                        paymentSucceeded = true;
                    else
                    {
                        TempData["ErrorMessage"] = "Payment failed to process.";
                        return Page();
                    }
                }
                catch (Stripe.StripeException ex)
                {
                    TempData["ErrorMessage"] = $"Stripe error: {ex.Message}";
                    return Page();
                }
            }

            if (User.IsInRole(SD.AdminRole))
            {
                if (PaymentMethod == "Cash")
                {
                    paymentSucceeded = true;
                    paymentMethod = SD.CashPayment;
                }
                else if (PaymentMethod == "Check")
                {
                    paymentSucceeded = true;
                    paymentMethod = SD.CheckPayment;
                }
                else if (PaymentMethod == "Credit")
                {
                    paymentSucceeded = true;
                    paymentMethod = SD.CreditCardPayment;
                    if (TransactionApprovalID.HasValue)
                        tempTransactionApprovalId = TransactionApprovalID.Value;
                }
                else
                {
                    TempData["ErrorMessage"] = "Invalid payment method selected.";
                    return RedirectToPage("./Browse");
                }
            }

            _unitOfWork.Transaction.Add(new Transaction
            {
                ReservationId = Reservation.ReservationId,
                FeeId = baseTxnType.FeeId,
                PaymentMethod = paymentMethod,
                Amount = BaseAmount,
                TriggerType = baseTxnType.TriggerType,
                CalculationType = baseTxnType.CalculationType ?? default,
                TriggerRuleSnapshotJson = baseTxnType.TriggerRuleJson,
                Description = baseTxnType.DisplayLabel,
                TransactionDateTime = DateTime.UtcNow,
                PreviouslyRefunded = false,
                IsPaid = paymentSucceeded,
                TransactionApprovalId = tempTransactionApprovalId
            });

            if (PaymentMethod == SD.CheckPayment)
            {
                _unitOfWork.Check.Add(new Check
                {
                    TransactionId = _unitOfWork.Transaction
                        .Get(t => t.Reservation.ReservationId == Reservation.ReservationId
                            && t.FeeId == baseTxnType.FeeId
                            && t.PaymentMethod == paymentMethod).TransactionId,
                    CheckNumber = Check.CheckNumber,
                    CheckDateTime = Check.CheckDateTime,
                    Amount = Check.Amount
                });
            }

            var triggeredTxns = await _transactionService.ApplyTriggeredTransactionsAsync(Reservation, BaseAmount, NumberOfDays);
            foreach (var summary in triggeredTxns)
            {
                var txn = new Transaction
                {
                    ReservationId = Reservation.ReservationId,
                    FeeId = summary.FeeId,
                    PaymentMethod = paymentMethod,
                    Amount = summary.Amount,
                    Description = summary.Label,
                    TriggerType = TriggerType.Automatic,
                    TransactionDateTime = DateTime.UtcNow,
                    IsPaid = true
                };

                _unitOfWork.Transaction.Add(txn);
            }

            await _unitOfWork.CommitAsync();

            TotalAmount = BaseAmount + triggeredTxns.Sum(t => t.Amount);

            TempData["SuccessMessage"] = FromGuestFlow
                ? "Thank you for creating an account! Your reservation has been successfully created and payment processed."
                : "Your reservation has been successfully created and payment processed.";

            return RedirectToPage("./Confirmation", new { reservationId = Reservation.ReservationId });
        }

        private IEnumerable<ApplicationCore.Models.Price> GetAdjustedPrices(string siteType, DateTime start, DateTime end)
        {
            var dbPrices = _unitOfWork.Price.GetAll(
                p => p.SiteType.Name == siteType &&
                     (!(end < p.StartDate) && (p.EndDate == null || !(start > p.EndDate)))
            ).OrderBy(p => p.StartDate);

            return dbPrices.Select(p => new ApplicationCore.Models.Price
            {
                PriceId = p.PriceId,
                SiteType = p.SiteType,
                SiteTypeId = p.SiteTypeId,
                StartDate = p.StartDate < start ? start : p.StartDate,
                EndDate = p.EndDate == null ? end : (p.EndDate > end ? end : p.EndDate),
                PricePerDay = p.PricePerDay
            }).ToList();
        }
    }
}
