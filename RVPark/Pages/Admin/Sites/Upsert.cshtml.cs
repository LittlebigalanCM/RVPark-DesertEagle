using ApplicationCore.Models;
using DocumentFormat.OpenXml.InkML;
using Infrastructure.Data;
using Infrastructure.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Org.BouncyCastle.Crypto;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RVPark.Pages.Admin.Sites
{
    [Authorize(Roles = SD.AdminRole)]
    public class UpsertModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UnitOfWork _unitOfWork;

        public UpsertModel(ApplicationDbContext context, UnitOfWork unitOfWork)
        {
            _context = context;
            _unitOfWork = unitOfWork;
        }

        [BindProperty]
        public Site Site { get; set; } = new Site();

        public IEnumerable<SelectListItem> SiteTypeList { get; set; } = new List<SelectListItem>();

        public async Task<IActionResult> OnGetAsync(int? siteId)
        {
            var siteTypes = _unitOfWork.SiteType.GetAll();
            SiteTypeList = siteTypes.Select(s => new SelectListItem
            {
                Value = s.SiteTypeId.ToString(),
                Text = s.Name
            });

            if ((!siteId.HasValue || siteId == 0))
            {
                // Creating new site
                Site = new Site();
                return Page();
            }

            // Editing existing site
            Site = await _context.Site.FirstOrDefaultAsync(m => m.SiteId == siteId);

            if (Site == null)
            {
                return NotFound();
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                // Re-populate dropdown if validation fails
                var siteTypes = _unitOfWork.SiteType.GetAll();
                SiteTypeList = siteTypes.Select(s => new SelectListItem
                {
                    Value = s.SiteTypeId.ToString(),
                    Text = s.Name
                });

                return Page();
            }

            if (Site.SiteId == 0)
            {
                // Create new site
                _context.Site.Add(Site);
            }
            else
            {
                var existingSite = await _context.Site.FirstOrDefaultAsync(s => s.SiteId == Site.SiteId);

                existingSite.Name = Site.Name;
                existingSite.Description = Site.Description;
                existingSite.SiteTypeId = Site.SiteTypeId;
                existingSite.TrailerMaxSize = Site.TrailerMaxSize;
                existingSite.IsLocked = Site.IsLocked;
                existingSite.IsHandicappedAccessible = Site.IsHandicappedAccessible;
            }

            await _context.SaveChangesAsync();
            return RedirectToPage("./Index");
        }
    }
}