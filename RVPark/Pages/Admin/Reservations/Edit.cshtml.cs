using ApplicationCore.Interfaces;
using ApplicationCore.Models;
using Infrastructure.Data;
using Infrastructure.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RVPark.Pages.Admin.Reservations
{
    [Authorize(Roles = SD.AdminRole + "," + SD.StaffRole + "," + SD.CampHostRole)]
    public class EditModel : PageModel
    {
        private readonly UnitOfWork _unitOfWork;
        private readonly IEmailSender _emailSender;

        public EditModel(UnitOfWork unitOfWork, IEmailSender emailSender)
        {
            _unitOfWork = unitOfWork;
            _emailSender = emailSender;
        }

        [BindProperty]
        public Reservation Reservation { get; set; }

        [BindProperty]
        public int NumberOfVehicles { get; set; }

        [BindProperty]
        public string VehiclePlates { get; set; } = "";

        public string CustomerName { get; set; }
        public IEnumerable<SelectListItem> SiteList { get; set; }
        public IEnumerable<SiteType> SiteTypes { get; set; }
        public Dictionary<int, IEnumerable<Price>> SiteTypePrices { get; set; }
        public decimal OriginalTotal { get; set; }
        public Document? PCSOrders { get; set; }
        public Document? DisabilityDocument { get; set; }

        public decimal? CurrentSitePrice { get; set; }
        public DateTime OriginalStartDate { get; set; }
        public DateTime OriginalEndDate { get; set; }

        public IActionResult OnGet(int id)
        {
            Reservation = _unitOfWork.Reservation.Get(
                r => r.ReservationId == id,
                includes: "UserAccount,Site,Site.SiteType"
            );

            if (Reservation == null)
            {
                return NotFound();
            }

            NumberOfVehicles = Reservation.NumberOfVehicles;
            VehiclePlates = Reservation.VehiclePlates ?? "";

            CustomerName = $"{Reservation.UserAccount.FirstName} {Reservation.UserAccount.LastName}";

            var sites = _unitOfWork.Site.GetAll();
            SiteList = sites.Select(s => new SelectListItem
            {
                Value = s.SiteId.ToString(),
                Text = !string.IsNullOrEmpty(s.Name) ? $"{s.Name} ({s.SiteType.Name})" : $"Site #{s.SiteId}",
                Selected = s.SiteId == Reservation.SiteId
            });

            SiteTypes = _unitOfWork.SiteType.GetAll().Where(st => st.SiteTypeId != 5);

            //siteTypePrices = SiteTypes.ToDictionary(st => st.SiteTypeId, st => GetCurrentPriceForSiteType(st.SiteTypeId));

            // Get pricing for each site type
            SiteTypePrices = new Dictionary<int, IEnumerable<Price>>();
            foreach (var st in SiteTypes)
            {
                var prices = _unitOfWork.Price.GetAll(
                    p => p.SiteTypeId == st.SiteTypeId
                ).OrderByDescending(p => p.StartDate);
                SiteTypePrices[st.SiteTypeId] = prices;
            }

            OriginalTotal = CalculateReservationCost(Reservation.SiteId, Reservation.StartDate, Reservation.EndDate);
            CurrentSitePrice = OriginalTotal;

            if (Reservation.RequiresPCS == true)
                PCSOrders = _unitOfWork.Document.Get(d => d.ReservationId == Reservation.ReservationId && d.DocType == SD.PCSDocument);

            if (Reservation.RequiresDisability == true)
                DisabilityDocument = _unitOfWork.Document.Get(d => d.ReservationId == Reservation.ReservationId && d.DocType == SD.DisabilityDocument);

            OriginalStartDate = Reservation.StartDate;
            OriginalEndDate = Reservation.EndDate;

            return Page();
        }

        public IActionResult OnPost(bool cancelReservation = false)
        {
            if (cancelReservation)
            {
                var reservationToCancel = _unitOfWork.Reservation.Get(
                    r => r.ReservationId == Reservation.ReservationId
                );

                if (reservationToCancel != null)
                {
                    _unitOfWork.Reservation.Delete(reservationToCancel);
                    _unitOfWork.Commit();
                    TempData["SuccessMessage"] = "Reservation successfully cancelled.";
                    return RedirectToPage("./Index");
                }
            }

            if (!ModelState.IsValid)
            {
                ReloadLists();
                return Page();
            }

            var originalReservation = _unitOfWork.Reservation.Get(
                r => r.ReservationId == Reservation.ReservationId,
                false,
                "Site,Site.SiteType"
            );

            if (originalReservation == null)
                return NotFound();

            var site = _unitOfWork.Site.Get(s => s.SiteId == Reservation.SiteId, includes: "SiteType");
            if (site == null)
            {
                ModelState.AddModelError(string.Empty, "Selected site does not exist.");
                ReloadLists();
                return Page();
            }

            int maxVehicles = site.SiteTypeId switch
            {
                3 or 7 => 2,
                1 or 2 => 3,
                _ => 2
            };

            if (NumberOfVehicles > maxVehicles)
            {
                ModelState.AddModelError(string.Empty, $"This site allows a maximum of {maxVehicles} vehicles.");
                ReloadLists();
                return Page();
            }

            if (Reservation.TrailerLength.HasValue &&
                site.TrailerMaxSize.HasValue &&
                Reservation.TrailerLength > site.TrailerMaxSize)
            {
                ModelState.AddModelError(string.Empty,
                    $"Trailer length ({Reservation.TrailerLength} ft) exceeds site maximum ({site.TrailerMaxSize} ft).");
                ReloadLists();
                return Page();
            }

            var overlappingReservations = _unitOfWork.Reservation.GetAll(
                r => r.SiteId == Reservation.SiteId &&
                     r.EndDate > Reservation.StartDate &&
                     r.StartDate < Reservation.EndDate &&
                     r.ReservationStatus != SD.CancelledReservation &&
                     r.ReservationStatus != SD.CompleteReservation &&
                     r.ReservationId != Reservation.ReservationId
            );

            if (overlappingReservations.Any())
            {
                ModelState.AddModelError(string.Empty, "The selected site is already booked for these dates.");
                ReloadLists();
                return Page();
            }

            bool datesChanged = originalReservation.StartDate != Reservation.StartDate ||
                                originalReservation.EndDate != Reservation.EndDate;
            bool siteChanged = originalReservation.SiteId != Reservation.SiteId;

            if (datesChanged || siteChanged)
            {
                decimal originalCost = CalculateReservationCost(
                    originalReservation.SiteId,
                    originalReservation.StartDate,
                    originalReservation.EndDate);

                decimal newCost = CalculateReservationCost(
                    Reservation.SiteId,
                    Reservation.StartDate,
                    Reservation.EndDate);

                decimal feeDifference = newCost - originalCost;

                if (feeDifference != 0)
                {
                    var siteType = site.SiteType;
                    var feeName = feeDifference > 0 ? SD.OtherFeeName : SD.RefundName;
                    var fee = _unitOfWork.Fee.Get(t => t.Name == feeName);

                    if (fee == null)
                    {
                        ModelState.AddModelError("", $"Transaction type '{feeName}' not found.");
                        ReloadLists();
                        return Page();
                    }

                    var originalTransaction = _unitOfWork.Transaction.Get(
                        t => t.ReservationId == originalReservation.ReservationId &&
                             t.Description.Contains("Base Reservation"));

                    var transaction = new Transaction
                    {
                        ReservationId = Reservation.ReservationId,
                        Amount = feeDifference,
                        Description = feeDifference > 0
                            ? $"Additional charge due to reservation change for {site?.Name} ({siteType?.Name})"
                            : $"Refund due to reservation change for {site?.Name} ({siteType?.Name})",
                        PreviouslyRefunded = false,
                        FeeId = fee.FeeId,
                        TransactionDateTime = DateTime.UtcNow,
                        PaymentMethod = originalTransaction?.PaymentMethod == SD.CreditCardPayment ? SD.CreditCardPayment : "N/A"
                    };

                    _unitOfWork.Transaction.Add(transaction);
                }
            }

            originalReservation.SiteId = Reservation.SiteId;
            originalReservation.StartDate = Reservation.StartDate;
            originalReservation.EndDate = Reservation.EndDate;
            originalReservation.TrailerLength = Reservation.TrailerLength;
            originalReservation.NumberOfVehicles = NumberOfVehicles;
            originalReservation.VehiclePlates = VehiclePlates?.Trim();

            originalReservation.Site = _unitOfWork.Site.Get(s => s.SiteId == Reservation.SiteId);

            if (originalReservation.StartDate > DateTime.Now)
            {
                originalReservation.ReservationStatus = SD.UpcomingReservation;
            }
            else if (originalReservation.EndDate > DateTime.Now)
            {
                originalReservation.ReservationStatus = SD.ActiveReservation;
            }
            else
            {
                originalReservation.ReservationStatus = SD.CompleteReservation;
            }

            bool originalPCS = originalReservation.RequiresPCS == true;
            bool originalDisability = originalReservation.RequiresDisability == true;

            if (originalReservation.Site.IsHandicappedAccessible)
            {
                if (!originalDisability)
                {
                    SendDisabilityEmailAsync();
                    _unitOfWork.Document.Add(new Document
                    {
                        Filepath = "",
                        FileName = "",
                        ContentType = "",
                        DocType = SD.DisabilityDocument,
                        IsApproved = false,
                        ReservationId = Reservation.ReservationId
                    });
                }
                originalReservation.RequiresDisability = true;
                originalReservation.ReservationStatus = SD.PendingReservation;
            }
            else
            {
                originalReservation.RequiresDisability = false;
            }

            int nights = (Reservation.EndDate - Reservation.StartDate).Days;
            bool requiresPCS = nights > 180;

            if (requiresPCS)
            {
                if (!originalPCS)
                {
                    SendPCSOrdersEmailAsync();
                    _unitOfWork.Document.Add(new Document
                    {
                        Filepath = "",
                        FileName = "",
                        ContentType = "",
                        DocType = SD.PCSDocument,
                        IsApproved = false,
                        ReservationId = Reservation.ReservationId
                    });
                }
                originalReservation.RequiresPCS = true;
                originalReservation.ReservationStatus = SD.PendingReservation;
            }
            else
            {
                originalReservation.RequiresPCS = false;
            }

            _unitOfWork.Reservation.Update(originalReservation);
            _unitOfWork.Commit();

            TempData["SuccessMessage"] = "Reservation updated successfully.";
            return RedirectToPage("./Index");
        }

        private decimal CalculateReservationCost(int siteId, DateTime startDate, DateTime endDate)
        {
            var site = _unitOfWork.Site.Get(s => s.SiteId == siteId, includes: "SiteType");

            if (site == null || !site.SiteTypeId.HasValue)
                return 0;

            var currentPrices = _unitOfWork.Price.GetAll(
                p => p.SiteTypeId == site.SiteTypeId &&
                     p.StartDate <= Reservation.StartDate &&
                     (p.EndDate == null || p.EndDate >= Reservation.StartDate)
            ).OrderByDescending(p => p.StartDate);

            int nights = (endDate - startDate).Days;

            var adjustedPrices = currentPrices.Select(p => new
            {
                Start = p.StartDate < startDate ? startDate : p.StartDate,
                End = (p.EndDate == null || p.EndDate > endDate) ? endDate : p.EndDate.Value,
                p.PricePerDay
            }).ToList();

            decimal total = 0;

            for (int i = 0; i < nights; i++)
            {
                var date = startDate.AddDays(i);
                var price = adjustedPrices
                    .Where(p => p.Start <= date && p.End >= date)
                    .Select(p => p.PricePerDay)
                    .FirstOrDefault();

                total += price;
            }

            return total;
        }

        private decimal GetCurrentPriceForSiteType(int siteTypeId)
        {
            var referenceDate = Reservation?.StartDate.Date ?? DateTime.Today;

            var currentPrice = _unitOfWork.Price.GetAll(
                p => p.SiteTypeId == siteTypeId &&
                     p.StartDate <= referenceDate &&
                     (p.EndDate == null || p.EndDate >= referenceDate)
            ).OrderByDescending(p => p.StartDate).FirstOrDefault();

            return currentPrice?.PricePerDay ?? 50.0m;
        }

        private void ReloadLists()
        {
            var sites = _unitOfWork.Site.GetAll();
            SiteList = sites.Select(s => new SelectListItem
            {
                Value = s.SiteId.ToString(),
                Text = !string.IsNullOrEmpty(s.Name) ? $"{s.Name} ({s.SiteType.Name})" : $"Site #{s.SiteId}",
                Selected = s.SiteId == Reservation.SiteId
            });

            SiteTypes = _unitOfWork.SiteType.GetAll().Where(st => st.SiteTypeId != 5);

            //siteTypePrices = SiteTypes.ToDictionary(st => st.SiteTypeId, st => GetCurrentPriceForSiteType(st.SiteTypeId));

            // Get pricing for each site type
            SiteTypePrices = new Dictionary<int, IEnumerable<Price>>();
            foreach (var st in SiteTypes)
            {
                var prices = _unitOfWork.Price.GetAll(
                    p => p.SiteTypeId == st.SiteTypeId
                ).OrderByDescending(p => p.StartDate);
                SiteTypePrices[st.SiteTypeId] = prices;
            }
        }

        private async Task SendPCSOrdersEmailAsync()
        {
            var client = _unitOfWork.UserAccount.Get(u => u.Id == Reservation.UserId);
            var userEmail = client.Email;
            var emailSubject = $"Desert Eagle RV Park - ACTION REQUIRED (Reservation #{Reservation.ReservationId}) PCS ORDERS";
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
    }
}
