using ApplicationCore.Interfaces;
using ApplicationCore.Models;
using Infrastructure.Data;
using Infrastructure.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace RVPark.Pages.Admin.Photos
{
    [Authorize(Roles = SD.AdminRole + "," + SD.StaffRole + "," + SD.CampHostRole)]
    public class EditModel : PageModel
    {
        private readonly UnitOfWork _unitOfWork;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly string[] _allowedExtensions = { ".jpg", ".jpeg", ".png", ".gif" };
        private const int _maxFileSize = 5 * 1024 * 1024; // 5MB

        public EditModel(UnitOfWork unitOfWork, IWebHostEnvironment webHostEnvironment)
        {
            _unitOfWork = unitOfWork;
            _webHostEnvironment = webHostEnvironment;
        }

        [BindProperty]
        public Photo Photo { get; set; } = default!;

        public IActionResult OnGet(int photoId)
        {
            var photo = _unitOfWork.Photo.Get(p => p.Id == photoId);

            if (photo == null)
            {
                return NotFound();
            }

            Photo = photo;
            ViewData["SiteId"] = new SelectList(_unitOfWork.Site.GetAll(), "SiteId", "Name");
            return Page();
        }

        public IActionResult OnPost(IFormFile photoFile)
        {

            string webRootPath = _webHostEnvironment.WebRootPath;
            var files = HttpContext.Request.Form.Files;
            var existingPhoto = _unitOfWork.Photo.Get(p => p.Id == Photo.Id, true);

            if (files.Count > 0)
            {
                //create a unique identifier for the image name
                string fileName = Guid.NewGuid().ToString();

                //create variable to hold the path to the images/menuitems subfolder
                var uploads = Path.Combine(webRootPath, @"images\sitePhotos\");

                //get and preserve the extnsion type
                var extension = Path.GetExtension(files[0].FileName);

                //if the item in the database has an image
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                var imagePath = Path.Combine(webRootPath, existingPhoto.Name.TrimStart('\\'));
#pragma warning restore CS8602 // Dereference of a possibly null reference.

                //if the image exists, physically delete
                if (System.IO.File.Exists(imagePath))
                {
                    System.IO.File.Delete(imagePath);
                }

                var fullPath = uploads + fileName + extension;

                using var fileStream = System.IO.File.Create(fullPath);
                files[0].CopyTo(fileStream);
                //associate the image to the MenuItem Object
                Photo.Name = @"\images\sitePhotos\" + fileName + extension;
            }
            else
            {
                //add imagefrom the existing database item to the item we're updating
                Photo.Name = existingPhoto.Name;
            }

            _unitOfWork.Photo.Update(Photo);
            _unitOfWork.Commit();

            return RedirectToPage("./Index", new { siteId = Photo.SiteId });
        }
    }
}

