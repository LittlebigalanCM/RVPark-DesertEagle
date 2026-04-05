using ApplicationCore.Models;
using Infrastructure.Data;
using Infrastructure.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;

namespace RVPark.Pages.Client.Portal
{
    [Authorize]
    public class IndexModel : PageModel
    {
        private readonly UnitOfWork _unitOfWork;

        public IndexModel(UnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public Reservation? NextReservation { get; private set; }
        public Site? NextSite { get; private set; }
        public List<Photo> SitePhotos { get; private set; } = new();
        public string? NextSiteTypeName { get; private set; }
        public int Nights { get; private set; }
        public decimal TotalPaid { get; private set; }
        public decimal TotalAmount { get; private set; }
        public decimal BalanceDue => TotalAmount - TotalPaid;

        [BindProperty(SupportsGet = true)]
        public int ReservationId { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToPage("/Account/Login");
            }

            NextReservation = _unitOfWork.Reservation.GetAll(r =>
                r.UserId == userId &&
                r.EndDate >= DateTime.UtcNow &&
                r.ReservationStatus != SD.CancelledReservation &&
                r.ReservationStatus != SD.CompleteReservation)
                .OrderBy(r => r.StartDate)
                .FirstOrDefault();

            if (NextReservation == null)
            {
                TempData["ErrorMessage"] = "You have no upcoming reservations.";
                return RedirectToPage("/Client/Reservations/Index");
            }

            NextSite = _unitOfWork.Site.Get(s => s.SiteId == NextReservation.SiteId);
            if (NextSite == null)
            {
                TempData["ErrorMessage"] = "Site information for this reservation is missing.";
                return RedirectToPage("/Client/Reservations/Index");
            }

            var siteType = _unitOfWork.SiteType.Get(st => st.SiteTypeId == NextSite.SiteTypeId);
            NextSiteTypeName = siteType?.Name;

            Nights = Math.Max(1, (NextReservation.EndDate - NextReservation.StartDate).Days);

            var txns = _unitOfWork.Transaction.GetAll(t => t.ReservationId == NextReservation.ReservationId).ToList();

            TotalAmount = txns.Sum(t => t.Amount);

            TotalPaid = txns.Where(t => t.IsPaid).Sum(t => t.Amount);

            SitePhotos = (await _unitOfWork.Photo.GetAllAsync(p => p.SiteId == NextReservation.SiteId)).ToList();

            if (!SitePhotos.Any())
            {
                SitePhotos.Add(new Photo { Name = "/images/site-placeholder.jpg" });
            }

            return Page();
        }
    }
}
