using ApplicationCore.Models;
using Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;

namespace RVPark.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OccupancyReportController : Controller
    {
        private readonly UnitOfWork _unitOfWork;

        public OccupancyReportController(UnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        [HttpGet("GetAll")]
        public IActionResult GetAll(DateTime startDate, DateTime endDate)
        {
            if (startDate > endDate)
            {
                return BadRequest("Start date cannot be after end date.");
            }

            var reservations = _unitOfWork.Reservation.GetAll(
                predicate: null,
                includes: "UserAccount,Site");

            var results = reservations.Where(r =>
                    r.StartDate <= endDate && r.EndDate >= startDate);

            var data = results.Select(r => new
            {
                customerName = $"{r.UserAccount?.FirstName} {r.UserAccount?.LastName}",
                contact = !string.IsNullOrEmpty(r.UserAccount?.PhoneNumber) ? r.UserAccount.PhoneNumber : r.UserAccount?.Email,
                siteName = !string.IsNullOrEmpty(r.Site?.Name) ? r.Site.Name : $"Site #{r.SiteId}",
                checkIn = r.StartDate, // Map StartDate to checkIn
                checkOut = r.EndDate,  // Map EndDate to checkOut
                status = DateTime.UtcNow > r.EndDate ? "Complete" :
                         (DateTime.UtcNow >= r.StartDate && DateTime.UtcNow <= r.EndDate) ? "In Progress" : "Upcoming"
            });

            return Json(new { data });
        }
    }
}