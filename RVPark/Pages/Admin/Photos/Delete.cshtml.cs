using ApplicationCore.Interfaces;
using ApplicationCore.Models;
using Infrastructure.Data;
using Infrastructure.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.IO;
using System.Threading.Tasks;

namespace RVPark.Pages.Admin.Photos
{
    [Authorize(Roles = SD.AdminRole + "," + SD.StaffRole + "," + SD.CampHostRole)]
    public class DeleteModel : PageModel
    {
        private readonly UnitOfWork _unitOfWork;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public DeleteModel(UnitOfWork unitOfWork, IWebHostEnvironment webHostEnvironment)
        {
            _unitOfWork = unitOfWork;
            _webHostEnvironment = webHostEnvironment;
        }

        [BindProperty]
        public Photo Photo { get; set; } = default!;

        public IActionResult OnGet(int photoId)
        {
            var photo = _unitOfWork.Photo.Get(
                predicate: p => p.Id == photoId,
                includes: "Site"
            );

            if (photo == null)
            {
                return NotFound();
            }

            Photo = photo;
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var photo = _unitOfWork.Photo.Get(p => p.Id == Photo.Id);

            if (photo == null)
            {
                return NotFound();
            }

            // Delete the physical file first
            var imgPath = Path.Combine(_webHostEnvironment.WebRootPath,
                    photo.Name.Trim('\\'));
            if (System.IO.File.Exists(imgPath)) //image physically there
            {
                System.IO.File.Delete(imgPath);
            }

            // Then delete the database record
            _unitOfWork.Photo.Delete(photo);
            await _unitOfWork.CommitAsync();

            return RedirectToPage("./Index");
        }
    }
}

