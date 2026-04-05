using ApplicationCore.Enums;
using ApplicationCore.Models;
using Infrastructure.Data;
using Infrastructure.Services;
using Infrastructure.Utilities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RVPark.Pages.Client.Reservations
{
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
        public int ChosenSiteId { get; set; }
        
        public string CurrentSiteName { get; set; }

        public string CustomerName { get; set; }
        public IEnumerable<SelectListItem> SiteList { get; set; }
        public decimal CurrentTotal { get; set; }
        public IEnumerable<ApplicationCore.Models.SiteType> SiteTypes { get; set; }
        public Dictionary<int, IEnumerable<Price>> SiteTypePrices { get; set; }


        [BindProperty]
        public int NumberOfVehicles { get; set; }
        [BindProperty]
        public string VehiclePlates { get; set; } = "";
        [BindProperty]
        public List<string> VehiclePlateList { get; set; } = new();


        public IActionResult OnGet(int id)
        {
            Reservation = _unitOfWork.Reservation.Get(
                r => r.ReservationId == id,
                includes: "UserAccount,Site,Site.SiteType"
            );

            if (Reservation == null || Reservation.Site == null || Reservation.UserAccount == null)
            {
                return NotFound();
            }

            NumberOfVehicles = Reservation.NumberOfVehicles;
            VehiclePlates = Reservation.VehiclePlates ?? "";

            if (!string.IsNullOrWhiteSpace(Reservation.VehiclePlates))
            {
                VehiclePlateList = Reservation.VehiclePlates
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(p => p.Trim())
                    .ToList();

                NumberOfVehicles = VehiclePlateList.Count;
            }
            else
            {
                VehiclePlateList = new List<string>();
            }

            CustomerName = $"{Reservation.UserAccount.FirstName} {Reservation.UserAccount.LastName}";

            var sites = _unitOfWork.Site.GetAll(s => s.SiteTypeId != 5 && s.SiteTypeId != 6).ToList(); // Materialize sites
            SiteList = sites.Select(s => new SelectListItem
            {
                Value = s.SiteId.ToString(),
                Text = !string.IsNullOrEmpty(s.Name) ? s.Name : $"Site #{s.SiteId}",
                Selected = s.SiteId == Reservation.SiteId
            }).ToList();

            SiteTypes = _unitOfWork.SiteType.GetAll(st => st.SiteTypeId != 5 && st.SiteTypeId != 6).ToList(); // Materialize site types

            // Get pricing for each site type
            SiteTypePrices = new Dictionary<int, IEnumerable<Price>>();
            foreach (var st in SiteTypes)
            {
                var prices = _unitOfWork.Price.GetAll(
                    p => p.SiteTypeId == st.SiteTypeId
                ).OrderByDescending(p => p.StartDate);
                SiteTypePrices[st.SiteTypeId] = prices;
            }

            // Pretty sure it's fine to comment out all this price-related stuff? The current total is calculated in a different way now, so I don't
            // think any of this was doing anything. Leaving it here for now in case I'm wrong. - Matt
            // Fetch all prices for the relevant site types in one query
            //var siteTypeIds = SiteTypes.Select(st => st.SiteTypeId).ToList();
            //var prices = _unitOfWork.Price.GetAll(
            //    p => siteTypeIds.Contains(p.SiteTypeId) &&
            //         p.StartDate <= DateTime.Now &&
            //         (p.EndDate == null || p.EndDate >= DateTime.Now)
            //).GroupBy(p => p.SiteTypeId)
            // .Select(g => g.OrderByDescending(p => p.StartDate).FirstOrDefault())
            // .ToList();

            //foreach (var type in SiteTypes)
            //{
            //    var price = prices.FirstOrDefault(p => p?.SiteTypeId == type.SiteTypeId);
            //    siteTypePrices[type.SiteTypeId] = price != null ? (decimal)price.PricePerDay : 50.0m;
            //}

            CurrentTotal = CalculateCurrentCost(Reservation);

            CurrentSiteName = !string.IsNullOrWhiteSpace(Reservation.Site?.Name)
                ? Reservation.Site.Name
                : $"Site #{Reservation.SiteId}";

            return Page();
        }

        public IActionResult OnPost(bool cancelReservation = false)
        {
            if (cancelReservation)
            {
                var reservationToCancel = _unitOfWork.Reservation.Get(
                    r => r.ReservationId == Reservation.ReservationId,
                   includes: null
                );


                if (reservationToCancel == null)
                {
                    return NotFound();
                }

                reservationToCancel.ReservationStatus = SD.CancelledReservation;
                _unitOfWork.Reservation.Update(reservationToCancel);
                _unitOfWork.Commit();

                TempData["SuccessMessage"] = "Reservation successfully cancelled.";
                return RedirectToPage("./Index");
            }

            string plate1 = Request.Form["VehiclePlate1"];
            string plate2 = Request.Form["VehiclePlate2"];
            string plate3 = Request.Form["VehiclePlate3"];

            VehiclePlateList = new List<string>
                {
                    plate1?.Trim(),
                    plate2?.Trim(),
                    plate3?.Trim(),
                }
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToList();

            NumberOfVehicles = VehiclePlateList.Count;
            VehiclePlates = string.Join(",", VehiclePlateList);

            Reservation.VehiclePlates = VehiclePlates;
            Reservation.NumberOfVehicles = NumberOfVehicles;
            ModelState.Remove("Reservation.VehiclePlates");
            ModelState.Remove("Reservation.NumberOfVehicles");
            ModelState.Remove("NumberOfVehicles");
            ModelState.Remove("VehiclePlates");

            if (!ModelState.IsValid)
            {
                // Reload the full reservation with navigation properties
                var fullReservation = _unitOfWork.Reservation.Get(
                    r => r.ReservationId == Reservation.ReservationId,
                    includes: "UserAccount,Site,Site.SiteType"
                );

                if (fullReservation != null)
                {
                    CustomerName = $"{fullReservation.UserAccount.FirstName} {fullReservation.UserAccount.LastName}";
                    // Copy the user account back to our bound Reservation object so the view has it
                    Reservation.UserAccount = fullReservation.UserAccount;
                }
                else
                {
                    CustomerName = "Unknown";
                }

                var sites = _unitOfWork.Site.GetAll(s => s.SiteTypeId != 5 && s.SiteTypeId != 6).ToList();
                SiteList = sites.Select(s => new SelectListItem
                {
                    Value = s.SiteId.ToString(),
                    Text = !string.IsNullOrEmpty(s.Name) ? s.Name : $"Site #{s.SiteId}",
                    Selected = s.SiteId == Reservation.SiteId
                }).ToList();

                SiteTypes = _unitOfWork.SiteType.GetAll(st => st.SiteTypeId != 5 && st.SiteTypeId != 6).ToList();
                //siteTypePrices.Clear();

                var siteTypeIds = SiteTypes.Select(st => st.SiteTypeId).ToList();
                var prices = _unitOfWork.Price.GetAll(
                    p => siteTypeIds.Contains(p.SiteTypeId) &&
                         p.StartDate <= DateTime.Now &&
                         (p.EndDate == null || p.EndDate >= DateTime.Now)
                ).GroupBy(p => p.SiteTypeId)
                 .Select(g => g.OrderByDescending(p => p.StartDate).FirstOrDefault())
                 .ToList();

                //foreach (var type in SiteTypes)
                //{
                //    var price = prices.FirstOrDefault(p => p?.SiteTypeId == type.SiteTypeId);
                //    siteTypePrices[type.SiteTypeId] = price != null ? (decimal)price.PricePerDay : 50.0m;
                //}

                CurrentTotal = CalculateCurrentCost(fullReservation);

                return Page();
            }

            var site = _unitOfWork.Site.Get(s => s.SiteId == ChosenSiteId, includes: "SiteType");
            if (site == null)
            {
                ModelState.AddModelError(string.Empty, "Selected site does not exist.");
                return OnGet(Reservation.ReservationId);
            }

            Reservation.SiteId = site.SiteId;

            var siteType = _unitOfWork.SiteType.Get(st => st.SiteTypeId == site.SiteTypeId);
            if (siteType == null)
            {
                ModelState.AddModelError(string.Empty, "Site type not found.");
                return OnGet(Reservation.ReservationId);
            }

            var overlappingReservations = _unitOfWork.Reservation.GetAll(
                predicate: r => r.SiteId == Reservation.SiteId &&
                                r.ReservationId != Reservation.ReservationId &&
                                r.EndDate > Reservation.StartDate &&
                                r.StartDate < Reservation.EndDate &&
                                r.ReservationStatus != SD.CancelledReservation &&
                                r.ReservationStatus != SD.CompleteReservation
            ).ToList(); // Materialize overlapping reservations

            if (overlappingReservations.Any())
            {
                ModelState.AddModelError(string.Empty, "The selected site is already booked for these dates.");
                return OnGet(Reservation.ReservationId);
            }

            if (Reservation.TrailerLength.HasValue && site.TrailerMaxSize.HasValue)
            {
                if (Reservation.TrailerLength > site.TrailerMaxSize)
                {
                    ModelState.AddModelError(string.Empty, $"Trailer length ({Reservation.TrailerLength} ft) exceeds site maximum ({site.TrailerMaxSize} ft).");
                    return OnGet(Reservation.ReservationId);
                }
            }

            var originalReservation = _unitOfWork.Reservation.Get(
                r => r.ReservationId == Reservation.ReservationId,
                includes: null 
            );


            if (originalReservation == null)
            {
                return NotFound();
            }

            bool datesChanged = originalReservation.StartDate != Reservation.StartDate ||
                                originalReservation.EndDate != Reservation.EndDate;
            bool siteChanged = originalReservation.SiteId != Reservation.SiteId;

            if (datesChanged || siteChanged)
            {
                decimal originalCost = CalculateCurrentCost(originalReservation);

                decimal newCost = CalculateReservationCost(
                    Reservation.SiteId,
                    Reservation.StartDate,
                    Reservation.EndDate);

                decimal feeDifference = newCost - originalCost;

                if (feeDifference != 0)
                {
                    var transaction = new Transaction
                    {
                        ReservationId = Reservation.ReservationId,
                        Amount = (decimal)feeDifference,
                        PaymentMethod = "N/A", // To be updated with actual payment method in future
                        Description = feeDifference > 0
                            ? $"Additional charge due to reservation change for {site.Name} ({siteType.Name})"
                            : $"Refund due to reservation change for {site.Name} ({siteType.Name})",
                        PreviouslyRefunded = false,
                        FeeId = feeDifference > 0 ? 1 : 5, // 1 = payment, 5 = refund. Previously, refund was set here as 2, which is incorrect, I think?
                        TransactionDateTime = DateTime.UtcNow
                    };

                    _unitOfWork.Transaction.Add(transaction);
                }
            }

            if (Reservation.StartDate.Date == DateTime.Now.Date)
            {
                originalReservation.ReservationStatus = SD.ActiveReservation;
            }
            else if (Reservation.StartDate.Date > DateTime.Now.Date)
            {
                originalReservation.ReservationStatus = SD.UpcomingReservation;
            }

            originalReservation.SiteId = Reservation.SiteId;
            originalReservation.StartDate = Reservation.StartDate;
            originalReservation.EndDate = Reservation.EndDate;
            originalReservation.TrailerLength = Reservation.TrailerLength;
            originalReservation.ReservationStatus = Reservation.ReservationStatus;
            originalReservation.NumberOfVehicles = NumberOfVehicles;
            originalReservation.VehiclePlates = VehiclePlates;


            //Check and update the flags
            //If it has a value and the value is true, then set the flag to true. Otherwise, false
            bool originalPCS = originalReservation.RequiresPCS.HasValue && originalReservation.RequiresPCS == true;
            bool originalDisability = originalReservation.RequiresDisability.HasValue && originalReservation.RequiresDisability == true;

            //if this isn't the same as the original, check for truthiness
            //if either is true, send email and mark to pending
            //if false and used to be true, send a follow up email about it being removed

            //this is after I assigned the originalReservation a new site, so...
            originalReservation.Site = _unitOfWork.Site.Get(s => s.SiteId == Reservation.SiteId);
            if (originalReservation.Site.IsHandicappedAccessible)
            {
                //send email if originally, didn't have a disability
                if (!originalDisability)
                {
                    SendDisabilityEmailAsync();

                    //make a PCS document entry here linked to this reservation
                    _unitOfWork.Document.Add(new Document
                    {
                        Filepath = "", //filepath will be updated when user uploads the document
                        FileName = "",
                        ContentType = "",
                        DocType = SD.DisabilityDocument,
                        IsApproved = false,
                        ReservationId = Reservation.ReservationId
                    });
                }
                originalReservation.RequiresDisability = true;
                //change to pending
                originalReservation.ReservationStatus = SD.PendingReservation;
            }
            else
            {
                originalReservation.RequiresDisability = false;
            }
            //Check for PCS
            int nights = (Reservation.EndDate - Reservation.StartDate).Days;
            bool requiresPCS = nights > 180 ? true : false;
            if (requiresPCS)
            {
                //send email if originally, didn't have a disability
                if (!originalPCS)
                {
                    SendPCSOrdersEmailAsync();

                    //make a PCS document entry here linked to this reservation
                    _unitOfWork.Document.Add(new Document
                    {
                        Filepath = "", //filepath will be updated when user uploads the document
                        FileName = "",
                        ContentType = "",
                        DocType = SD.PCSDocument,
                        IsApproved = false,
                        ReservationId = Reservation.ReservationId
                    });
                }
                originalReservation.RequiresPCS = true;
                //change to pending
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

        // Made to check the actual amount that was paid. If we re-calculated it every time, there's a good chance it'd be inaccurate.
        private decimal CalculateCurrentCost(Reservation reservation)
        {
            if (reservation == null)
            {
                return 0; // This should likely return an error instead, as it means the reservation doesn't exist at all
            }

            var nights = (reservation.EndDate.Date - reservation.StartDate.Date).Days;
            var transactions = _unitOfWork.Transaction.GetAll(t => t.ReservationId == reservation.ReservationId && (t.FeeId == 1 || t.FeeId == 5)).ToList(); // Get base reservation costs, plus any previous refunds or other changes. TODO: this will include refunds that are issued for other reasons! We need to think of a better way to do this in general
            return transactions.Where(t => !t.PreviouslyRefunded).Sum(t => t.Amount); // TODO: check that older Base Reservation transactions are being fully refunded, so that this actually works
        }

        private decimal CalculateReservationCost(int siteId, DateTime startDate, DateTime endDate)
        {
            var site = _unitOfWork.Site.Get(s => s.SiteId == siteId);
            if (site == null || !site.SiteTypeId.HasValue)
            {
                return 0;
            }

            var nights = (endDate.Date - startDate.Date).Days;

            // Get SiteType's related Price objects
            var prices = _unitOfWork.Price.GetAll(
                p => p.SiteTypeId == site.SiteTypeId &&
                (!(endDate < p.StartDate) && (p.EndDate == null || !(startDate > p.EndDate)))
            ).OrderBy(p => p.StartDate);

            //do this so that you're not impacting the EF tracked entities
            var adjustedPrices = prices.Select(p => new
            {
                Start = p.StartDate < startDate ? startDate : p.StartDate,
                End = p.EndDate == null ? endDate : (p.EndDate > endDate ? endDate : p.EndDate.Value),
                p.PricePerDay
            }).ToList();

            // Calculate the BaseAmount by iterating day-by-day
            decimal baseAmount = 0;
            for (int i = 0; i < nights; i++)
            {
                var date = startDate.AddDays(i);
                var priceForDate = adjustedPrices
                    .Where(p => p.Start <= date && p.End >= date)
                    .Select(p => p.PricePerDay)
                    .FirstOrDefault();
                baseAmount += priceForDate;
            }

            return baseAmount;
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

    }
}