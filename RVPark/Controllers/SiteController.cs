using ApplicationCore.Interfaces;
using ApplicationCore.Models;
using Infrastructure.Data;
using Infrastructure.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace RVPark.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SiteController : Controller
    {
        // Provides access to repositories and handles database commits
        private readonly UnitOfWork _unitOfWork;

        public SiteController(UnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        /// <summary>
        /// Returns all sites with basic type and size info.
        /// </summary>
        [HttpGet]
        public IActionResult Get()
        {
            // Fetch all sites including their associated SiteType
            var sites = _unitOfWork.Site.GetAll(
                predicate: null,
                includes: "SiteType"
            );

            // Shape the response for UI use
            var data = sites.Select(s => new
            {
                s.SiteId,
                s.Name,
                SiteTypeName = s.SiteType?.Name ?? "Unknown",
                s.Description,
                TrailerMaxSize = s.TrailerMaxSize.HasValue ? $"{s.TrailerMaxSize} ft" : "N/A"
            });

            return Json(new { data });
        }

        /// <summary>
        /// Retrieves a single site by ID.
        /// </summary>
        [HttpGet("{id}")]
        public IActionResult GetSite(int id)
        {
            // Load site with SiteType info
            var site = _unitOfWork.Site.Get(
                predicate: s => s.SiteId == id,
                includes: "SiteType"
            );

            if (site == null)
            {
                return NotFound();
            }

            return Json(site);
        }

        /// <summary>
        /// Returns all available SiteTypes.
        /// </summary>
        [HttpGet("GetSiteTypes")]
        public IActionResult GetSiteTypes()
        {
            var siteTypes = _unitOfWork.SiteType.GetAll();
            return Json(siteTypes);
        }

        /// <summary>
        /// Returns the max trailer size for a site.
        /// </summary>
        [HttpGet("GetSiteTrailerSize")]
        public IActionResult GetSiteTrailerSize(int siteId)
        {
            // Look up trailer size by site ID
            var size = _unitOfWork.Site.Get(s => s.SiteId == siteId).TrailerMaxSize;

            // Return 0 if null for easier frontend handling
            return Json(size ?? 0);
        }

        /// <summary>
        /// Returns all sites that are available for the given date range.
        /// </summary>
        [HttpGet("GetAvailableSites")]
        public IActionResult GetAvailableSites(string startDate, string endDate)
        {
            // Validate dates
            if (!DateTime.TryParse(startDate, out DateTime start) ||
                !DateTime.TryParse(endDate, out DateTime end))
            {
                return BadRequest("Invalid date format");
            }

            if (start > end)
                return BadRequest("Start date must be before or equal to end date");

            // Load all sites with SiteType info
            var allSites = _unitOfWork.Site.GetAll(
                predicate: null,
                includes: "SiteType"
            );

            // Find all reservations that overlap with the requested date range
            var overlappingReservations = _unitOfWork.Reservation.GetAll(
                predicate: r =>
                    r.EndDate >= start &&
                    r.StartDate <= end &&
                    r.ReservationStatus != SD.CancelledReservation &&
                    r.ReservationStatus != SD.CompleteReservation,
                includes: "Site"
            );

            // Build lists of unavailable site IDs and names
            var bookedSiteIds = overlappingReservations.Select(r => r.SiteId).ToList();
            var bookedSiteNames = overlappingReservations.Select(r => r.Site?.Name).ToList();

            // Filter out sites that are already booked
            var availableSites = allSites.Where(s =>
                !bookedSiteIds.Contains(s.SiteId) &&
                !bookedSiteNames.Contains(s.Name)
            );

            // Format the data for response
            var data = availableSites.Select(s => new
            {
                s.SiteId,
                s.Name,
                SiteTypeName = s.SiteType?.Name ?? "Unknown",
                s.Description,
                TrailerMaxSize = s.TrailerMaxSize.HasValue ? $"{s.TrailerMaxSize} ft" : "N/A"
            });

            return Json(new { data });
        }

        /// <summary>
        /// Creates a new Site.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] Site site)
        {
            if (site == null)
                return BadRequest("Site data is missing");

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                _unitOfWork.Site.Add(site);
                await _unitOfWork.CommitAsync();

                // Return result with CreatedAtAction for REST correctness
                return CreatedAtAction(nameof(GetSite), new { id = site.SiteId }, site);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred while creating the site: {ex.Message}");
            }
        }

        /// <summary>
        /// Updates an existing site.
        /// </summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> Edit(int id, [FromBody] Site site)
        {
            // Basic validation
            if (site == null || id != site.SiteId)
                return BadRequest("Invalid site data");

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            // Ensure site exists before updating
            var existingSite = _unitOfWork.Site.Get(s => s.SiteId == id);
            if (existingSite == null)
                return NotFound($"Site with ID {id} not found");

            try
            {
                _unitOfWork.Site.Update(site);
                await _unitOfWork.CommitAsync();

                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred while updating the site: {ex.Message}");
            }
        }

        /// <summary>
        /// Deletes a site, unless it has associated reservations.
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            // Load site
            var site = _unitOfWork.Site.Get(s => s.SiteId == id);
            if (site == null)
                return NotFound($"Site with ID {id} not found");

            // Cannot delete if reservations are tied to the site
            var reservations = _unitOfWork.Reservation.GetAll(r => r.SiteId == id);
            if (reservations.Any())
                return BadRequest("Cannot delete this site because it has associated reservations");

            try
            {
                _unitOfWork.Site.Delete(site);
                await _unitOfWork.CommitAsync();
                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred while deleting the site: {ex.Message}");
            }
        }
    }
}