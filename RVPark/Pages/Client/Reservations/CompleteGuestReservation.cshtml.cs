using ApplicationCore.Models;
using Infrastructure.Data;
using Infrastructure.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;
using System.Text.Json;

namespace RVPark.Pages.Client.Reservations
{
    [Authorize] // Require authentication for this page
    public class CompleteGuestReservationModel : PageModel
    {
        private readonly UnitOfWork _unitOfWork;

        public CompleteGuestReservationModel(UnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public IActionResult OnGet()
        {
            // Check if we have guest reservation data
            if (TempData["GuestReservationData"] == null)
            {
                TempData["ErrorMessage"] = "No reservation data found. Please start your reservation again.";
                return RedirectToPage("./Browse");
            }

            try
            {
                // Deserialize the guest reservation data
                var guestDataJson = TempData["GuestReservationData"].ToString();
                var guestData = JsonSerializer.Deserialize<GuestReservationData>(guestDataJson);

                // Check if the data is still valid (not too old)
                if (DateTime.UtcNow.Subtract(guestData.CreatedAt).TotalHours > 2)
                {
                    TempData["ErrorMessage"] = "Your reservation session has expired. Please start again.";
                    return RedirectToPage("./Browse");
                }

                // Validate the site is still available
                var site = _unitOfWork.Site.Get(s => s.SiteId == guestData.SiteId);
                if (site == null)
                {
                    TempData["ErrorMessage"] = "The selected site is no longer available. Please make a new selection.";
                    return RedirectToPage("./Browse");
                }

                // Check for overlapping reservations (in case someone booked while they were logging in)
                var overlappingReservations = _unitOfWork.Reservation.GetAll(
                    predicate: r => r.SiteId == guestData.SiteId &&
                                    r.EndDate > guestData.StartDate &&
                                    r.StartDate < guestData.EndDate &&
                                    r.ReservationStatus != SD.CancelledReservation &&
                                    r.ReservationStatus != SD.CompleteReservation
                );

                if (overlappingReservations.Any())
                {
                    TempData["ErrorMessage"] = "Sorry, the selected site was booked by someone else while you were logging in. Please make a new selection.";
                    return RedirectToPage("./Browse");
                }

                // Get the current user ID
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                {
                    TempData["ErrorMessage"] = "Authentication error. Please try logging in again.";
                    return RedirectToPage("/Account/Login", new { area = "Identity" });
                }

                // Create the reservation object
                var reservation = new Reservation
                {
                    UserId = userId,
                    SiteId = guestData.SiteId,
                    StartDate = guestData.StartDate,
                    EndDate = guestData.EndDate,
                    TrailerLength = guestData.TrailerLength
                };

                // Get site type for pricing
                var siteType = _unitOfWork.SiteType.Get(st => st.SiteTypeId == site.SiteTypeId);
                var currentPrice = _unitOfWork.Price.GetAll(
                    p => p.SiteTypeId == site.SiteTypeId &&
                         p.StartDate <= DateTime.Now &&
                         (p.EndDate == null || p.EndDate >= DateTime.Now)
                ).OrderByDescending(p => p.StartDate).FirstOrDefault();

                decimal dailyRate = currentPrice?.PricePerDay ?? 50.0m;

                // Store reservation data for the summary page (similar to the regular flow)
                TempData["ReservationData"] = JsonSerializer.Serialize(reservation);
                TempData["SiteId"] = guestData.SiteId;
                TempData["SiteName"] = site.Name;
                TempData["SiteType"] = siteType?.Name ?? "Unknown";
                TempData["DailyRate"] = dailyRate.ToString();
                TempData["StartDate"] = guestData.StartDate.ToString("yyyy-MM-dd");
                TempData["EndDate"] = guestData.EndDate.ToString("yyyy-MM-dd");
                TempData["TrailerLength"] = guestData.TrailerLength?.ToString() ?? "N/A";
                TempData["FromGuestFlow"] = "true"; // Flag to indicate this came from guest flow

                // Redirect to the summary page to complete the reservation
                return RedirectToPage("./Summary");
            }
            catch (Exception ex)
            {
                // Log the error if you have logging configured
                TempData["ErrorMessage"] = "An error occurred processing your reservation. Please start again.";
                return RedirectToPage("./Browse");
            }
        }
    }
}