using ApplicationCore.Interfaces;
using ApplicationCore.Models;
using Infrastructure.Data;
using Infrastructure.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace RVPark.Pages.Admin.Photos
{
    [Authorize(Roles = SD.AdminRole + "," + SD.StaffRole + "," + SD.CampHostRole)]
    public class DetailsModel : PageModel
    {
        private readonly UnitOfWork _unitOfWork;

        public DetailsModel(UnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        [BindProperty]
        public Photo Photo { get; set; }
        public Site Site { get; set; }

        public void OnGet(int photoId)
        {
 
            var photo = _unitOfWork.Photo.Get(p => p.Id== photoId);

            if (photo != null)
            {
                Photo = photo;
                Site = _unitOfWork.Site.Get(s => s.SiteId == Photo.SiteId);
                Page();
            }
            else
            {
                RedirectToPage("./Index");
            }
        }
    }
}

