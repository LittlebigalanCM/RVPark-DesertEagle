using ApplicationCore.Models;
using Infrastructure.Data;
using Infrastructure.Services;
using Infrastructure.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.IdentityModel.Tokens;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using System.Security.Claims;
using System.Text.Json;
using static Infrastructure.Services.EmailSender;

namespace RVPark.Pages.Client.Reservations
{
    [Authorize]
    public class ConfirmationModel : PageModel
    {
        private readonly UnitOfWork _unitOfWork;
        private readonly IEmailSender _emailSender;

        public ConfirmationModel(UnitOfWork unitOfWork, IEmailSender sender)
        {
            _unitOfWork = unitOfWork;
            _emailSender = sender;
        }

        [BindProperty]
        public Reservation Reservation { get; set; }
        public string SiteName { get; set; }
        public string SiteTypeName { get; set; }
        public decimal TotalAmount { get; set; }
        public string SuccessMessage { get; set; }
        public string SpecialRequests { get; set; }
        public List<Transaction> Transactions { get; set; }
        public Dictionary<string, object> DynamicData { get; set; } = new();
        public List<(string Label, int Units)> AllUnits { get; set; } = new();
        public List<(string Label, object Value)> TransactionFieldsWithLabels
            => AllUnits.Select(u => (u.Label, (object)u.Units)).ToList();

        [BindProperty(SupportsGet = true)]
        public int? ReservationId { get; set; }

        [TempData]
        public string? DynamicDataJson { get; set; }

        public IActionResult OnGet()
        {
            if (!ReservationId.HasValue || ReservationId == 0)
                return RedirectToPage("./Index");

            int reservationId = ReservationId.Value;
            SuccessMessage = TempData["SuccessMessage"]?.ToString();
            SpecialRequests = TempData["SpecialRequests"]?.ToString();

            Reservation = _unitOfWork.Reservation.Get(r => r.ReservationId == reservationId);
            if (Reservation == null)
                return NotFound();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            bool isAdmin = User.IsInRole(SD.AdminRole) || User.IsInRole(SD.StaffRole) || User.IsInRole(SD.CampHostRole);

            if (!isAdmin && Reservation.UserId != userId)
                return Forbid();

            var site = _unitOfWork.Site.Get(s => s.SiteId == Reservation.SiteId);
            var siteType = _unitOfWork.SiteType.Get(st => st.SiteTypeId == site.SiteTypeId);
            var transactions = _unitOfWork.Transaction.GetAll(t => t.ReservationId == reservationId);

            SiteName = site.Name;
            SiteTypeName = siteType.Name;
            TotalAmount = transactions.Sum(t => t.Amount);
            Transactions = transactions.ToList();

            // Deserialize dynamic field data
            if (!string.IsNullOrWhiteSpace(DynamicDataJson))
            {
                DynamicData = JsonSerializer.Deserialize<Dictionary<string, object>>(DynamicDataJson) ?? new();
                TempData.Keep(nameof(DynamicDataJson));
            }
            else
            {
                DynamicData = Reservation.DynamicData ?? new Dictionary<string, object>();
            }

            // ✅ Updated: Use IsEnabled instead of IsTransactionField
            var dynamicFields = _unitOfWork.CustomDynamicField
                .GetAll(f => !f.IsDeleted && f.IsEnabled)
                .ToDictionary(f => f.FieldName, f => f);

            AllUnits.Clear();

            foreach (var kvp in DynamicData)
            {
                if (int.TryParse(kvp.Value?.ToString(), out int units) && units > 0)
                {
                    string label = dynamicFields.TryGetValue(kvp.Key, out var field)
                        ? (string.IsNullOrWhiteSpace(field.ConfirmationLabel)
                            ? (string.IsNullOrWhiteSpace(field.DisplayLabel) ? kvp.Key : field.DisplayLabel)
                            : field.ConfirmationLabel)
                        : kvp.Key;

                    AllUnits.Add((label, units));
                }
            }

            SendReceiptEmailAsync();

            //additional email if there's PCS orders involved
            //check is there is a value for PCSOrders, and it has to be true for this to send
            if (Reservation.RequiresPCS.HasValue && Reservation.RequiresPCS == true)
            {
                SendPCSOrdersEmailAsync();
            }

            //another additional email if the user requires a disability accommodation
            //check is there is a value for RequiresDisability, and it has to be true for this to send
            if (Reservation.RequiresDisability.HasValue && Reservation.RequiresDisability == true)
            {
                SendDisabilityEmailAsync();
            }


            return Page();
        }

        public IActionResult OnGetPrintReceipt(int reservationId)
        {
            var reservation = _unitOfWork.Reservation.Get(r => r.ReservationId == reservationId);
            if (reservation == null)
                return NotFound();

            List<Transaction> transactions = _unitOfWork.Transaction.GetAll(t => t.ReservationId == reservationId).ToList();
            decimal totalAmount = transactions.Sum(t => t.Amount);

            var document = new PdfDocument();
            var page = document.AddPage();
            var gfx = XGraphics.FromPdfPage(page);

            var titleFont = new XFont("Verdana", 14, XFontStyle.Bold);
            var sectionFont = new XFont("Verdana", 10, XFontStyle.Bold);
            var bodyFont = new XFont("Verdana", 10);

            double y = 40;
            gfx.DrawString("Reservation Receipt", titleFont, XBrushes.Black, new XRect(0, y, page.Width, 20), XStringFormats.TopCenter);
            y += 30;
            gfx.DrawString($"Reservation Number: #{reservation.ReservationId}", sectionFont, XBrushes.Black, new XPoint(40, y));
            y += 20;
            gfx.DrawString($"Reservation Dates: {reservation.StartDate:MM/dd/yyyy} - {reservation.EndDate:MM/dd/yyyy}", bodyFont, XBrushes.Black, new XPoint(40, y));
            y += 30;

            gfx.DrawString("Charges", sectionFont, XBrushes.Black, new XPoint(40, y));
            y += 20;

            foreach (var transaction in transactions)
            {
                string line = $"{transaction.Description} .......... Amount: ${transaction.Amount:F2}";
                gfx.DrawString(line, bodyFont, XBrushes.Black, new XPoint(60, y));
                y += 15;
            }

            y += 10;
            gfx.DrawLine(XPens.Black, 40, y, page.Width - 40, y);
            y += 10;
            gfx.DrawString($"Total Paid: ${totalAmount:F2}", sectionFont, XBrushes.Black, new XPoint(60, y));

            y += 30;
            gfx.DrawString("Thank you for your stay at Desert Eagle RV Park!", bodyFont, XBrushes.Black, new XPoint(40, y));

            var stream = new MemoryStream();
            document.Save(stream, false);
            stream.Position = 0;

            return File(stream, "application/pdf", $"Receipt_{reservation.ReservationId}.pdf");
        }

        private async Task SendReceiptEmailAsync()
        {
            var client = _unitOfWork.UserAccount.Get(u => u.Id == Reservation.UserId);
            var userEmail = client?.Email;
            if (string.IsNullOrWhiteSpace(userEmail)) return;

            var nights = (Reservation.EndDate.Date - Reservation.StartDate.Date).Days;

            var model = new ReservationReceiptModel
            {
                ReservationId = Reservation.ReservationId,
                GuestName = $"{client?.FirstName} {client?.LastName}".Trim(),
                StartDate = Reservation.StartDate,
                EndDate = Reservation.EndDate,
                NumberOfNights = nights,
                SiteName = SiteName,
                SiteType = SiteTypeName,
                Status = Reservation.ReservationStatus ?? "Unknown",
                TotalPaid = TotalAmount
            };

            await _emailSender.SendReservationReceiptAsync(userEmail, model);
        }
         private async Task SendPCSOrdersEmailAsync()
        {
            var client = _unitOfWork.UserAccount.Get(u => u.Id == Reservation.UserId);
            var userEmail = client.Email;
            var emailSubject = $"Desert Eagle RV Park - ACTION REQUIRED (Reservation #{Reservation.ReservationId}) PCS ORDERS";
            //please fill this out when a page is created for uploading PCS orders
            //var link = $"{Request.Scheme}://{Request.Host}/Client/Reservations/UploadPCSOrders?reservationId={Reservation.ReservationId}";

            var emailBody = $@"<!DOCTYPE html>
                <html>
                <head>
                    <meta charset=""utf-8"" />
                    <title>{emailSubject}</title>
                </head>
                <body style=""font-family: Arial, sans-serif; background-color: #f4f4f4; margin: 0; padding: 0; line-height: 1.6;"">
                    <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" border=""0"">
                        <tr>
                            <td align=""center"" style=""padding: 20px 0;"">
                                <table role=""presentation"" width=""600"" cellspacing=""0"" cellpadding=""0"" border=""0"" style=""border-collapse: collapse; background-color: #ffffff; border-radius: 8px; box-shadow: 0 4px 10px rgba(0,0,0,0.1);"">
                            
                                    <tr>
                                        <td align=""center"" bgcolor=""#3a8dd6"" style=""padding: 30px 20px; color: #ffffff; font-size: 24px; font-weight: bold; border-radius: 8px 8px 0 0;"">
                                            Desert Eagle RV Park
                                        </td>
                                    </tr>
                                    <tr>
                                        <td style=""padding: 30px 40px 30px 40px;"">
                                            <h1 style=""color: #333333; font-size: 22px; margin: 0 0 20px 0;"">
                                                Action Required!
                                            </h1>
                                            <p style=""color: #555555; font-size: 16px; margin: 0 0 20px 0;"">
                                                You recently made a reservation (Reservation #{Reservation.ReservationId}) that exceeded <strong>180 days.</strong> To be able to stay this long, <strong>you are required to provide proof of your PCS orders.</strong> Until then, your reservation will appear as <strong>Pending</strong> in your account.
                                                </p>
                                            <p style=""color: #555555; font-size: 16px; margin: 0 0 20px 0;"">
                                                If this is an error, please change your reservation dates in the customer portal or contact our support team for assistance.
                                            </p>
                                            <p style=""color: #555555; font-size: 16px; margin: 0 0 20px 0;"">
                                                Please reply to this email with a copy of your PCS orders attached, or click the button below to upload them securely.
                                            </p>
                                            <p style=""color: #555555; font-size: 16px; margin: 0 0 20px 0;"">
                                                Thank you for making a reservation with us. We look forward to seeing you.
                                            </p>
                                            
                                        </td>
                                    </tr>
                                    <tr>
                                        <td align=""center"" bgcolor=""#eeeeee"" style=""padding: 20px 40px; font-size: 12px; color: #777777; border-radius: 0 0 8px 8px;"">
                                            <p style=""margin: 0;"">
                                                Desert Eagle RV Park | Nellis Air Force Base
                                            </p>
                                        </td>
                                    </tr>
                                </table>
                            </td>
                        </tr>
                    </table>
                </body>
                </html>";

            /*
             Should you ever add in a page for uploading PCS orders, you can uncomment this section and add the button back in.

            <table role=""presentation"" cellspacing=""0"" cellpadding=""0"" border=""0"">
                    <tr>
                        <td align=""center"" bgcolor=""#28a745"" style=""border-radius: 5px;"">
                            <a href=""{link}"" target=""_blank"" style=""font-size: 16px; font-weight: bold; text-decoration: none; color: #ffffff; padding: 12px 25px; border: 1px solid #28a745; display: inline-block; border-radius: 5px;"">
                                CONFIRM PCS ORDERS
                            </a>
                        </td>
                    </tr>
                </table>
                <p style=""color: #555555; font-size: 14px; margin: 25px 0 0 0;"">
                    If the button above doesn't work, you can copy and paste the following link into your web browser:
                </p>
                <p style=""font-size: 12px; color: #777777; word-break: break-all;"">
                    <a href=""{link}"" style=""color:#0047AB;"">{link}</a>
                </p>
             */


            if (!userEmail.IsNullOrEmpty())
            {
                await _emailSender.SendEmailAsync(userEmail, emailSubject, emailBody);
            }
        }

        private async Task SendDisabilityEmailAsync()
        {
            var client = _unitOfWork.UserAccount.Get(u => u.Id == Reservation.UserId);
            var userEmail = client.Email;
            var emailSubject = $"Desert Eagle RV Park - ACTION REQUIRED (Reservation #{Reservation.ReservationId}) DISABILITY DOCUMENTATION";
            //please fill this out when a page is created for uploading PCS orders
            //var link = $"{Request.Scheme}://{Request.Host}/Client/Reservations/UploadDisability?reservationId={Reservation.ReservationId}";

            var emailBody = $@"<!DOCTYPE html>
                <html>
                <head>
                    <meta charset=""utf-8"" />
                    <title>{emailSubject}</title>
                </head>
                <body style=""font-family: Arial, sans-serif; background-color: #f4f4f4; margin: 0; padding: 0; line-height: 1.6;"">
                    <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" border=""0"">
                        <tr>
                            <td align=""center"" style=""padding: 20px 0;"">
                                <table role=""presentation"" width=""600"" cellspacing=""0"" cellpadding=""0"" border=""0"" style=""border-collapse: collapse; background-color: #ffffff; border-radius: 8px; box-shadow: 0 4px 10px rgba(0,0,0,0.1);"">
                            
                                    <tr>
                                        <td align=""center"" bgcolor=""#3a8dd6"" style=""padding: 30px 20px; color: #ffffff; font-size: 24px; font-weight: bold; border-radius: 8px 8px 0 0;"">
                                            Desert Eagle RV Park
                                        </td>
                                    </tr>
                                    <tr>
                                        <td style=""padding: 30px 40px 30px 40px;"">
                                            <h1 style=""color: #333333; font-size: 22px; margin: 0 0 20px 0;"">
                                                Action Required!
                                            </h1>
                                            <p style=""color: #555555; font-size: 16px; margin: 0 0 20px 0;"">
                                                You recently made a reservation (Reservation #{Reservation.ReservationId}) at a site that is a <strong>handicapped-accessible site.</strong> To be able to stay at this site, <strong>you are required to provide proof of your disability through documentation.</strong> Until then, your reservation will appear as <strong>Pending</strong> in your account.
                                                </p>
                                            <p style=""color: #555555; font-size: 16px; margin: 0 0 20px 0;"">
                                                If this is an error, please change your reservation site in the customer portal or contact our support team for assistance.
                                            </p>
                                            <p style=""color: #555555; font-size: 16px; margin: 0 0 20px 0;"">
                                                Please reply to this email with a copy of your disability documentation attached, or click the button below to upload them securely.
                                            </p>
                                            <p style=""color: #555555; font-size: 16px; margin: 0 0 20px 0;"">
                                                Thank you for making a reservation with us. We look forward to seeing you.
                                            </p>
                                        </td>
                                    </tr>
                                    <tr>
                                        <td align=""center"" bgcolor=""#eeeeee"" style=""padding: 20px 40px; font-size: 12px; color: #777777; border-radius: 0 0 8px 8px;"">
                                            <p style=""margin: 0;"">
                                                Desert Eagle RV Park | Nellis Air Force Base
                                            </p>
                                        </td>
                                    </tr>
                                </table>
                            </td>
                        </tr>
                    </table>
                </body>
                </html>";

            // Should you ever add in a page for uploading disability documentation, you can uncomment this section and add the button back in.
            /*
             <table role=""presentation"" cellspacing=""0"" cellpadding=""0"" border=""0"">
                <tr>
                    <td align=""center"" bgcolor=""#28a745"" style=""border-radius: 5px;"">
                        <a href=""{link}"" target=""_blank"" style=""font-size: 16px; font-weight: bold; text-decoration: none; color: #ffffff; padding: 12px 25px; border: 1px solid #28a745; display: inline-block; border-radius: 5px;"">
                            CONFIRM DISABILITY DOCUMENTATION
                        </a>
                    </td>
                </tr>
            </table>
            <p style=""color: #555555; font-size: 14px; margin: 25px 0 0 0;"">
                If the button above doesn't work, you can copy and paste the following link into your web browser:
            </p>
            <p style=""font-size: 12px; color: #777777; word-break: break-all;"">
                <a href=""{link}"" style=""color:#0047AB;"">{link}</a>
            </p>
             */

            if (!userEmail.IsNullOrEmpty())
            {
                await _emailSender.SendEmailAsync(userEmail, emailSubject, emailBody);
            }
        }


        public string GetConfirmationLabel(string key)
        {
            var field = _unitOfWork.CustomDynamicField.Get(f => f.FieldName == key && !f.IsDeleted);
            return string.IsNullOrWhiteSpace(field?.ConfirmationLabel)
                ? key
                : field.ConfirmationLabel;
        }

        private static int ToInt(object? v)
        {
            if (v == null) return 0;

            if (v is int i) return i;
            if (v is long l) return (int)l;
            if (v is double d) return (int)d;
            if (v is decimal m) return (int)m;
            if (v is string s && int.TryParse(s, out var si)) return si;

            if (v is JsonElement je)
            {
                return je.ValueKind switch
                {
                    JsonValueKind.Number => je.TryGetInt32(out var n) ? n : 0,
                    JsonValueKind.String => int.TryParse(je.GetString(), out var ns) ? ns : 0,
                    _ => 0
                };
            }

            return 0;
        }

    }//end class
}
