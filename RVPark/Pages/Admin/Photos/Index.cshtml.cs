using ApplicationCore.Interfaces;
using ApplicationCore.Models;
using Infrastructure.Data;
using Infrastructure.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RVPark.Pages.Admin.Photos
{
    [Authorize(Roles = SD.AdminRole + "," + SD.StaffRole + "," + SD.CampHostRole)]
    public class IndexModel : PageModel
    {
        private readonly UnitOfWork _unitOfWork;

        public IndexModel(UnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        //public List<Photo> Photo { get; set; }
        public IList<Photo> Photo { get; set; }
        //adding a comment to hopfully force a commit
        public Site Site { get; set; }

        public IActionResult OnGet(int? siteId)
        {
            var siteTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
{
    "Premium Pull-Through",
    "Premium Back-In",
    "Standard",
    "Tent/Overflow",
    "Short Term Storage",
    "Wagon Wheel Pull-Through",
    "Partial Hookup"
};
            //-It sorts by each Site type and gives an example-
            var allPhotos = _unitOfWork.Photo
    .GetAll(predicate: null, includes: "Site,Site.SiteType")
    .ToList();

            Photo = allPhotos
    .Where(p => p.Site != null
             && p.Site.SiteType != null
             && siteTypes.Contains(p.Site.SiteType.Name))
    .GroupBy(p => p.Site.SiteType.Name)
    .Select(g => g.OrderByDescending(p => p.Id).First())
    .ToList();
            return Page();
        }
    }
}

