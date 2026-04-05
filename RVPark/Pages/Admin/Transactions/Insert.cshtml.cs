using ApplicationCore.Models;
using Infrastructure.Data;
using Infrastructure.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Text;
using Stripe.Checkout;
using Infrastructure.Services;
using ApplicationCore.Enums;

namespace RVPark.Pages.Admin.Transactions
{
    [Authorize(Roles = SD.AdminRole + "," + SD.StaffRole + "," + SD.CampHostRole)]
    public class InsertModel : PageModel
    {
        private readonly UnitOfWork _unitofWork;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly IEmailSender _emailSender;
        private readonly StripeSettings _stripeSettings;
        private readonly IConfiguration _config;

        [BindProperty]
        public Transaction Transaction { get; set; }
        [BindProperty]
        public bool SendEmail { get; set; }

        public IEnumerable<SelectListItem> ReservationList { get; set; }
        public IEnumerable<SelectListItem> FeesList { get; set; }

        public InsertModel(
            UnitOfWork unitofWork,
            IWebHostEnvironment webHostEnvironment,
            IEmailSender emailSender,
            IOptions<StripeSettings> stripeSettings,
            IConfiguration configuration)
        {
            _unitofWork = unitofWork;
            _webHostEnvironment = webHostEnvironment;
            _emailSender = emailSender;
            _stripeSettings = stripeSettings.Value;
            _config = configuration;
        }

        public void OnGet(int reservationId)
        {
            var reservations = _unitofWork.Reservation.GetAll();
            var Fees = _unitofWork.Fee.GetAll(t => !t.IsEnabled && t.TriggerType == TriggerType.Manual);

            if (reservationId != 0)
            {
                reservations = reservations.Where(r => r.ReservationId == reservationId);
            }

            ReservationList = reservations.Select(r => new SelectListItem
            {
                Value = r.ReservationId.ToString(),
                Text = $"Reservation#{r.ReservationId} - Reservation Name: " +
                       $"{_unitofWork.UserAccount.Get(u => u.Id == r.UserId).FirstName} " +
                       $"{_unitofWork.UserAccount.Get(u => u.Id == r.UserId).LastName}"
            });

            FeesList = FeesList = Fees.Select(t => new SelectListItem
            {
                Value = t.FeeId.ToString(),
                Text = t.Name
            });

            Transaction = new();
        }



        public async Task<IActionResult> OnPostAsync(int? reservationId)
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            // Create the admin-added transaction (this is what we want the Payment page to pick up)
            _unitofWork.Transaction.Add(Transaction);
            _unitofWork.Commit(); // ensures TransactionId is set

            var reservation = _unitofWork.Reservation.Get(r => r.ReservationId == Transaction.ReservationId);
            if (reservation == null)
            {
                TempData["Error"] = "Reservation not found.";
                return RedirectToPage("./Index");
            }

            reservation.UserAccount = _unitofWork.UserAccount.Get(u => u.Id == reservation.UserId);
            reservation.Site = _unitofWork.Site.Get(s => s.SiteId == reservation.SiteId);

            // Absolute origin from request
            var baseUrl = $"{Request.Scheme}://{Request.Host}";

            // Links
            var receiptLink = $"{baseUrl}/Client/Reservations/Receipt?reservationId={Transaction.ReservationId}";

            // Friendly name/desc to include in the plural link (so details follow through)
            var txnType = _unitofWork.Fee.Get(t => t.FeeId == Transaction.FeeId);
            var name = txnType?.Name ?? "Reservation Charge";
            var desc = string.IsNullOrWhiteSpace(Transaction.Description)
                ? "Charge added by admin"
                : Transaction.Description;

            // Build the plural Payments link EXACTLY as requested, with two-decimal amount and URL-encoded details
            var amountString = Transaction.Amount.ToString("0.00", CultureInfo.InvariantCulture);
            var paymentLinkPlural =
                $"{baseUrl}/Client/Reservations/Payments" +
                $"?reservationId={Transaction.ReservationId}" +
                $"&amountDue={amountString}" +
                $"&name={Uri.EscapeDataString(name)}" +
                $"&description={Uri.EscapeDataString(desc)}";

            var guestEmail = reservation.UserAccount?.Email;

            if (SendEmail && !string.IsNullOrEmpty(guestEmail))
            {
                await SendEmailWithReceiptAsync(
                    reservation: reservation,
                    toEmail: guestEmail,
                    subject: BuildEmailSubjectForTransaction(Transaction),
                    receiptLink: receiptLink,
                    paymentPageLink: paymentLinkPlural, // <-- send plural link
                    isRefund: false
                );
            }

            TempData["Success"] = "Transaction created successfully." + (SendEmail ? " Guest has been notified." : "");
            return RedirectToPage("./Index", new { reservationId = reservationId });
        }

        // Subject shows the transaction type for clarity
        private string BuildEmailSubjectForTransaction(Transaction txn)
        {
            try
            {
                var txnType = _unitofWork.Fee.Get(t => t.FeeId == txn.FeeId);
                var typeName = txnType?.Name ?? "Transaction";
                return $"{typeName} Added to Your Reservation";
            }
            catch
            {
                return "Fee Added to Your Reservation";
            }
        }

        private async Task SendEmailWithReceiptAsync(
            Reservation reservation,
            string toEmail,
            string subject,
            string receiptLink,
            string paymentPageLink,
            bool isRefund)
        {
            var guestName = reservation.UserAccount?.FullName ?? "Guest";
            var siteName = reservation.Site?.Name ?? "Unknown Site";
            var startDate = reservation.StartDate.ToString("MMMM dd, yyyy");
            var endDate = reservation.EndDate.ToString("MMMM dd, yyyy");
            var reservationId = reservation.ReservationId;

            var body = new StringBuilder();
            body.AppendLine("<!DOCTYPE html><html><head><meta charset='utf-8'>");
            body.AppendLine("<style>");
            body.AppendLine("body{margin:0;padding:0;background:#fff;color:#000;font:14px/1.4 -apple-system,BlinkMacSystemFont,Segoe UI,Roboto,Helvetica,Arial,sans-serif;}");
            body.AppendLine(".wrap{max-width:640px;margin:0 auto;padding:16px 14px;}");
            body.AppendLine("h1,h2,h3,h4{margin:8px 0 6px 0;font-weight:600;color:#000;}");
            body.AppendLine("p{margin:6px 0;}");
            body.AppendLine(".box{margin-top:10px;border:1px solid #ddd;padding:10px;border-radius:6px;}");
            body.AppendLine(".grid{width:100%;border-collapse:collapse;}");
            body.AppendLine(".grid td{padding:6px 8px;vertical-align:top;border-top:1px solid #eee;}");
            body.AppendLine(".grid tr:first-child td{border-top:0;}");
            body.AppendLine(".label{width:42%;white-space:nowrap;font-weight:600;}");
            body.AppendLine("a{color:#000;text-decoration:underline;}");
            body.AppendLine(".footer{margin-top:12px;font-size:12px;color:#000;}");
            body.AppendLine("</style></head><body><div class='wrap'>");

            body.AppendLine($"<p>Dear {guestName},</p>");
            body.AppendLine(isRefund
                ? "<p>A refund has been processed for your reservation. Details are below.</p>"
                : "<p>A fee has been added to your reservation. Details are below.</p>");

            body.AppendLine("<div class='box'>");
            body.AppendLine("<table class='grid'>");
            body.AppendLine($"<tr><td class='label'>Reservation ID</td><td>{reservationId}</td></tr>");
            body.AppendLine($"<tr><td class='label'>Site</td><td>{siteName}</td></tr>");
            body.AppendLine($"<tr><td class='label'>Dates</td><td>{startDate} – {endDate}</td></tr>");
            body.AppendLine($"<tr><td class='label'>Receipt</td><td><a href='{receiptLink}'>View Receipt</a></td></tr>");
            body.AppendLine($"<tr><td class='label'>Transaction ID</td><td>{Transaction.TransactionId}</td></tr>");
            body.AppendLine($"<tr><td class='label'>Description</td><td>{(string.IsNullOrWhiteSpace(Transaction.Description) ? "—" : Transaction.Description)}</td></tr>");
            body.AppendLine($"<tr><td class='label'>Amount</td><td>{Transaction.Amount.ToString("C")}</td></tr>");
            body.AppendLine($"<tr><td class='label'>Type</td><td>{(Transaction.Fee?.DisplayLabel ?? "Unknown")}</td></tr>");
            body.AppendLine($"<tr><td class='label'>Status</td><td>{(Transaction.IsPaid ? "Paid" : "Unpaid")}</td></tr>");

            if (!string.IsNullOrEmpty(paymentPageLink) && !isRefund)
            {
                body.AppendLine("<tr><td class='label'>Outstanding Balance</td><td>A fee has been applied and your current balance is due.</td></tr>");
                body.AppendLine($"<tr><td class='label'>Pay Now</td><td><a href='{paymentPageLink}'>Pay Your Balance</a></td></tr>");
            }
            else if (isRefund)
            {
                body.AppendLine("<tr><td class='label'>Refund</td><td>A refund has been issued to your original payment method.</td></tr>");
            }

            body.AppendLine("</table>");
            body.AppendLine("</div>");

            body.AppendLine("<p class='footer'>Questions? Call (702) 643-3060 or email <a href='mailto:rvpark@example.com'>rvpark@example.com</a>.</p>");
            body.AppendLine("<p class='footer'>Sincerely,<br>Desert Eagle RV Park Team</p>");

            body.AppendLine("</div></body></html>");

            await _emailSender.SendEmailAsync(toEmail, subject, body.ToString());
        }


        public async Task<string> CreateStripePaymentLink(decimal amountDue, int reservationId, string email)
        {
            var baseUrl = _config["AppSettings:BaseUrl"];
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                baseUrl = $"{Request.Scheme}://{Request.Host}";
            }

            var options = new SessionCreateOptions
            {
                PaymentMethodTypes = new List<string> { "card" },
                LineItems = new List<SessionLineItemOptions>
                {
                    new SessionLineItemOptions
                    {
                        PriceData = new SessionLineItemPriceDataOptions
                        {
                            UnitAmountDecimal = amountDue * 100,
                            Currency = "usd",
                            ProductData = new SessionLineItemPriceDataProductDataOptions
                            {
                                Name = "Cancellation Fee"
                            }
                        },
                        Quantity = 1
                    }
                },
                Mode = "payment",
                SuccessUrl = $"{baseUrl}/Client/Reservations/Payment?reservationId={reservationId}&paid=true",
                CancelUrl = $"{baseUrl}/Client/Reservations/Payment?reservationId={reservationId}&paid=false",
                CustomerEmail = email
            };

            var service = new SessionService();
            var session = await service.CreateAsync(options);
            return session.Url;
        }
    }
}
