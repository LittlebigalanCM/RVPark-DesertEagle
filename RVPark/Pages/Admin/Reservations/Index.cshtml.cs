using ApplicationCore.Interfaces;
using ApplicationCore.Models;
using Infrastructure.Data;
using Infrastructure.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace RVPark.Pages.Admin.Reservations
{
    [Authorize(Roles = SD.AdminRole + "," + SD.StaffRole + "," + SD.CampHostRole)]
    public class IndexModel : PageModel
    {
        private readonly UnitOfWork _unitOfWork;

        [BindProperty]
        public string ReservationStatus { get; set; }

        public IEnumerable<SelectListItem> ReservationStatusList { get; set; }

        public IndexModel(UnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public void OnGet()
        {
            ReservationStatusList = new List<SelectListItem>
            {
                new SelectListItem { Value = SD.ActiveReservation, Text = SD.ActiveReservation },
                new SelectListItem { Value = SD.UpcomingReservation, Text = SD.UpcomingReservation },
                new SelectListItem { Value = SD.PendingReservation, Text = SD.PendingReservation },
                new SelectListItem { Value = SD.CompleteReservation, Text = SD.CompleteReservation },
                new SelectListItem { Value = SD.CancelledReservation, Text = SD.CancelledReservation }
            };
            // The page will load empty and be populated via AJAX
        }
    }
}
