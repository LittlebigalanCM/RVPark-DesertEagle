using ApplicationCore.Models;
using Infrastructure.Data;
using Infrastructure.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RVPark.Pages.Admin.Sites
{
    [Authorize(Roles = SD.AdminRole + "," + SD.StaffRole + "," + SD.CampHostRole)]
    public class DetailsModel : PageModel
    {
        private readonly Infrastructure.Data.ApplicationDbContext _context;
        private readonly UnitOfWork _unitOfWork;
        public DetailsModel(Infrastructure.Data.ApplicationDbContext context, UnitOfWork unitOfWork)
        {
            _context = context;
            _unitOfWork = unitOfWork;
        }

        [BindProperty]
        public Site Site { get; set; }

        public Photo Photo { get; set; }

        public IEnumerable<Photo> PhotoList { get; set; }
        public Price Price { get; set; }

        public async Task<IActionResult> OnGetAsync(int? siteId)
        {
            var site = _unitOfWork.Site.Get(m => m.SiteId == siteId, includes: "SiteType");
            if (site != null)
            {
                Site = site;
            }
           
            
            if (siteId == null)
            {
                return NotFound();
            }
            var image = _unitOfWork.Photo.GetAll(p => p.SiteId == siteId).FirstOrDefault();

            var images = _unitOfWork.Photo.GetAll(p => p.SiteId == siteId);

            var currentPrice = _unitOfWork.Price.GetAll(
                p => p.SiteTypeId == site.SiteTypeId &&
                p.StartDate <= DateTime.Now &&
                (p.EndDate == null || p.EndDate >= DateTime.Now)
            ).OrderByDescending(p => p.StartDate).FirstOrDefault();

            if (currentPrice != null)
            {
                Price = currentPrice;
            }

            if (images  != null)
            {
                PhotoList = images.ToList();
            }
            
            if (image != null)
            {
                Photo = image;
            }

            return Page();


        }
    }
}
