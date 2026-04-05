using ApplicationCore.Models;
using Infrastructure.Data;
using Infrastructure.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json;

namespace RVPark.Pages.Client.Reservations
{
    public class BrowseModel : PageModel
    {
        private readonly UnitOfWork _unitOfWork;

        public BrowseModel(UnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        [BindProperty]
        public DateTime StartDate { get; set; }

        [BindProperty]
        public DateTime EndDate { get; set; }

        [BindProperty]
        public int SiteId { get; set; }

        [BindProperty]
        [Range(0,70, ErrorMessage = "The largest RV we can accommodate is 70 ft")]
        public double? TrailerLength { get; set; }

        [BindProperty]
        public int NumberOfVehicles { get; set; } = 1;

        [BindProperty]
        public string? VehiclePlate1 { get; set; }

        [BindProperty]
        public string? VehiclePlate2 { get; set; }

        [BindProperty]
        public string? VehiclePlate3 { get; set; }

        public bool RequiresPCS { get; set; }
        public bool RequiresDisability { get; set; }


        public IEnumerable<ApplicationCore.Models.SiteType> SiteTypes { get; set; }
        public Dictionary<int, IEnumerable<Price>> SiteTypePrices { get; set; }

        public void OnGet()
        {
            // Set default dates
            StartDate = DateTime.UtcNow.Date.AddDays(1);
            EndDate = DateTime.UtcNow.Date.AddDays(8);
            
            // Get all site types
            SiteTypes = _unitOfWork.SiteType.GetAll();

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

        public IActionResult OnPost()
        {
                if (!ModelState.IsValid)
                {
                    // Repopulate data for the form
                    OnGet();
                    return Page();
                }

                // Validate SiteId
                if (SiteId <= 0)
                {
                    Console.WriteLine($"Invalid SiteId: {SiteId}");
                    ModelState.AddModelError("SiteId", "Please select a valid campsite.");
                    OnGet();
                    return Page();
                }

                // Validate dates
                if (StartDate >= EndDate)
                {
                    ModelState.AddModelError(string.Empty, "Check-in date must be before check-out date.");
                    OnGet();
                    return Page();
                }

                if (StartDate < DateTime.UtcNow.Date)
                { 
                    ModelState.AddModelError(string.Empty, "Check-in date cannot be in the past.");
                    OnGet();
                    return Page();
                }

                // Validate number of days for PCS orders requirement
                if(StartDate.AddDays(180) < EndDate) //if the end date is over 180 after the start date
                {
                    RequiresPCS = true;
                }
                else
                {
                    RequiresPCS = false;
                }

                // Validate site selection
                var site = _unitOfWork.Site.Get(s => s.SiteId == SiteId, includes: "SiteType");
                if (site == null)
                {
                    ModelState.AddModelError(string.Empty, "Please select a valid site.");
                    OnGet();
                    return Page();
                }

                // Check for overlapping reservations
                var overlappingReservations = _unitOfWork.Reservation.GetAll(
                    predicate: r => r.SiteId == SiteId &&
                                    r.EndDate > StartDate &&
                                    r.StartDate < EndDate &&
                                    r.ReservationStatus != SD.CancelledReservation &&
                                    r.ReservationStatus != SD.CompleteReservation
                );

                if (overlappingReservations.Any())
                { 
                    ModelState.AddModelError(string.Empty, "The selected site is no longer available for these dates. Please select different dates or another site.");
                    OnGet();
                    return Page();
                }

                // Check trailer length compatibility
                if (TrailerLength.HasValue && site.TrailerMaxSize.HasValue)
                {
                    if (TrailerLength > site.TrailerMaxSize)
                    {
                        ModelState.AddModelError(string.Empty, $"Trailer length ({TrailerLength} ft) exceeds site maximum ({site.TrailerMaxSize} ft).");
                        OnGet();
                        return Page();
                    }
                }

            int maxVehicles = site.SiteTypeId switch
            {
                3 or 7 => 2,
                1 or 2 => 3,
                _ => 2
            };

            if (NumberOfVehicles > maxVehicles)
            {
                ModelState.AddModelError(string.Empty, $"This site allows a maximum of {maxVehicles} vehicles with license plates.");
                OnGet();
                return Page();
            }

            if (User.IsInRole(SD.ClientRole))
            {
                List<string> plates = new();
                if (!string.IsNullOrWhiteSpace(VehiclePlate1)) plates.Add(VehiclePlate1.Trim());
                if (!string.IsNullOrWhiteSpace(VehiclePlate2)) plates.Add(VehiclePlate2.Trim());
                if (!string.IsNullOrWhiteSpace(VehiclePlate3)) plates.Add(VehiclePlate3.Trim());
                string vehiclePlateString = string.Join(", ", plates);

                // Create reservation object
                var Reservation = new ApplicationCore.Models.Reservation
                {
                    SiteId = SiteId,
                    StartDate = StartDate,
                    EndDate = EndDate,
                    TrailerLength = TrailerLength,
                    NumberOfVehicles = NumberOfVehicles,
                    VehiclePlates = vehiclePlateString,
                    ReservationStatus = SD.PendingReservation,
                };


                //check if selected site is a disability one. If so, set RequiresDisability to true
                if (site.IsHandicappedAccessible)
                {
                    Reservation.RequiresDisability = true;
                }
                else
                {
                    Reservation.RequiresDisability = false;
                }
                // Get site type and pricing
                var siteType = _unitOfWork.SiteType.Get(st => st.SiteTypeId == site.SiteTypeId);
                var dailyRate = GetCurrentPriceForSiteType(siteType.SiteTypeId);
                // Store reservation data in TempData for summary page
                TempData["ReservationData"] = System.Text.Json.JsonSerializer.Serialize(Reservation);
                TempData["SiteId"] = Reservation.SiteId;
                TempData["SiteName"] = site.Name;
                TempData["SiteType"] = siteType.Name;
                TempData["DailyRate"] = dailyRate.ToString();
                TempData["StartDate"] = StartDate.ToString("yyyy-MM-dd");
                TempData["EndDate"] = EndDate.ToString("yyyy-MM-dd");
                TempData["TrailerLength"] = TrailerLength?.ToString() ?? "N/A";
                RequiresDisability = Reservation.RequiresDisability == true;
                TempData["RequiresPCS"] = (Reservation.EndDate - Reservation.StartDate).TotalDays > 180 ? "true" : "false";
                TempData["RequiresDisability"] = RequiresDisability ? "true" : "false";
                TempData["AdminCreated"] = "false";

                //var user = _unitOfWork.UserAccount.Get(u => u.Id == claim.Value);
                //TempData["UserId"] = User.Claims.FirstOrDefault(c => c.Type == "UserId")?.Value;

                return RedirectToPage("./Summary");
            }
            else
            {
                List<string> plates = new();
                if (!string.IsNullOrWhiteSpace(VehiclePlate1)) plates.Add(VehiclePlate1.Trim());
                if (!string.IsNullOrWhiteSpace(VehiclePlate2)) plates.Add(VehiclePlate2.Trim());
                if (!string.IsNullOrWhiteSpace(VehiclePlate3)) plates.Add(VehiclePlate3.Trim());
                string vehiclePlateString = string.Join(", ", plates);

                // Store the guest reservation data in TempData for after login
                var guestReservationData = new GuestReservationData
                {
                    SiteId = SiteId,
                    StartDate = StartDate,
                    EndDate = EndDate,
                    TrailerLength = TrailerLength,
                    NumberOfVehicles = NumberOfVehicles,
                    VehiclePlates = vehiclePlateString,
                    CreatedAt = DateTime.UtcNow,
                };


                TempData["GuestReservationData"] = JsonSerializer.Serialize(guestReservationData);
                TempData["ReturnUrl"] = "/Client/Reservations/CompleteGuestReservation";

                // Redirect to login page with return URL for non signed in info
                return RedirectToPage("/Account/Login", new { area = "Identity", returnUrl = "/Client/Reservations/CompleteGuestReservation" });
            }
}

    // Helper method to get current price for a site type
    private decimal GetCurrentPriceForSiteType(int siteTypeId)
    {
        var currentPrice = _unitOfWork.Price.GetAll(
            p => p.SiteTypeId == siteTypeId &&
                 p.StartDate <= DateTime.Now &&
                 (p.EndDate == null || p.EndDate >= DateTime.Now)
        ).OrderByDescending(p => p.StartDate).FirstOrDefault();

        return currentPrice?.PricePerDay ?? 50.0m; // Default fallback rate
    }
}

    // Helper class to store guest reservation data
    public class GuestReservationData
    {
        public int SiteId { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public double? TrailerLength { get; set; }
        public int NumberOfVehicles { get; set; }
        public string? VehiclePlates { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}