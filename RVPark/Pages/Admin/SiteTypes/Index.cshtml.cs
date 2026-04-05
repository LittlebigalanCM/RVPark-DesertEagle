using ApplicationCore.Models;
using Infrastructure.Data;
using Infrastructure.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace RVPark.Pages.Admin.SiteTypes
{
    [Authorize(Roles = SD.AdminRole + "," + SD.StaffRole + "," + SD.CampHostRole)]
    public class IndexModel : PageModel
    {
        private readonly Infrastructure.Data.ApplicationDbContext _context;
        private readonly UnitOfWork _unitOfWork;

        public IndexModel(Infrastructure.Data.ApplicationDbContext context, UnitOfWork unitOfWork)
        {
            _context = context;
            _unitOfWork = unitOfWork;
        }

        public IList<ApplicationCore.Models.SiteType> SiteType { get; set; } = default!;

        // Dictionary to store current prices for each site type
        public Dictionary<int, decimal> SiteTypePrices { get; set; } = new Dictionary<int, decimal>();

        // Dictionary to store the latest price change for each site type
        public Dictionary<int, ApplicationCore.Models.Price> LatestPrices { get; set; } = new Dictionary<int, ApplicationCore.Models.Price>();

        public Dictionary<int, double?> SiteTypeTrailerMax { get; set; } = new();

        public async Task OnGetAsync()
        {
            // Load all site types
            SiteType = await _context.SiteType.ToListAsync();
            // load all sites once
            var allSites = await _context.Site.ToListAsync();


            var allPrices = await _context.Price.ToListAsync();

            // For each site type, find the most recent price
            foreach (var siteType in SiteType)
            {
                // Get the most recent price for this site type, regardless of date
                var latestPrice = allPrices
                   .Where(p => p.SiteTypeId == siteType.SiteTypeId && p.EndDate == null)
                   .OrderByDescending(p => p.StartDate)
                   .FirstOrDefault();

                //Get the current price for the site type
                var currentPrice = allPrices
                    .Where(p => p.SiteTypeId == siteType.SiteTypeId && ((p.StartDate <= DateTime.Today && p.EndDate >= DateTime.Today) || (p.StartDate <= DateTime.Today && p.EndDate == null)))
                    .OrderByDescending(p => p.StartDate)
                    .FirstOrDefault();

                if (latestPrice != null)
                {
                    LatestPrices[siteType.SiteTypeId] = latestPrice;
                }

                if (currentPrice != null)
                {
                    SiteTypePrices[siteType.SiteTypeId] = currentPrice.PricePerDay;
                }
                else
                {
                    // No price found for this site type
                    SiteTypePrices[siteType.SiteTypeId] = 0;
                }

                var lengths = allSites
    .Where(s => s.SiteTypeId == siteType.SiteTypeId && s.TrailerMaxSize.HasValue)
    .Select(s => s.TrailerMaxSize!.Value)
    .ToList();

                if (lengths.Any())
                {
                    // assume sites of same type share the same limit; use Max just in case
                    SiteTypeTrailerMax[siteType.SiteTypeId] = lengths.Max();
                }
                else
                {
                    SiteTypeTrailerMax[siteType.SiteTypeId] = null;
                }
            }
        }

        public async Task<IActionResult> OnPostLockUnlock(int id)
        {
            var siteType = _unitOfWork.SiteType.GetById(id);

            if(siteType.IsActive == null) //assume sitetype is not locked
            {
                siteType.IsActive = false;
            }
            else if(siteType.IsActive == false) //site type is locked
            {
                siteType.IsActive = true;
            }
            else
            {
                siteType.IsActive = false;
            }
            _unitOfWork.SiteType.Update(siteType);
            return RedirectToPage("./Index");
        }

        public IActionResult OnPostRevertPrice(int id)
        {
            if (id != 0)
            {
                var allPrices = _unitOfWork.Price.GetAll();

                var latestPrice = allPrices
                    .Where(p => p.SiteTypeId == id && p.EndDate == null)
                    .OrderByDescending(p => p.StartDate)
                    .FirstOrDefault();

                var previousPrice = allPrices
                        .Where(p => p.SiteTypeId == id && p != latestPrice && p.EndDate == latestPrice.StartDate.AddDays(-1))
                        .FirstOrDefault();

                if (latestPrice != null)
                {
                    if (previousPrice != null)
                    {
                        if (latestPrice.StartDate <= DateTime.Today)
                        {
                            return new JsonResult(new { success = false, message = "Can't revert, price range could be associated with reservations." });
                        }

                        _unitOfWork.Price.Delete(latestPrice);
                        previousPrice.EndDate = null;
                        _unitOfWork.Price.Update(previousPrice);
                        _unitOfWork.Commit();

                        return new JsonResult(new
                        {
                            success = true,
                            message = "Price range " +
                            latestPrice.StartDate.ToShortDateString() + "-" +
                            (latestPrice.EndDate.HasValue ? latestPrice.EndDate.Value.ToShortDateString() : "Ongoing") +
                            ": " + latestPrice.PricePerDay + " is reverted"
                        });
                    }
                    else
                    {
                        return new JsonResult(new { success = false, message = "Can't revert if only one price range for site type" });
                    }
                }
                else
                {
                    return new JsonResult(new { success = false, message = "No price set for this site type" });
                }
            }
            else
            {
                return new JsonResult(new { success = false, message = "Error reverting price range" });
            }
        }
    }
}