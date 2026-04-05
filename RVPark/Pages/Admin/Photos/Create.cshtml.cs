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
using System.Threading.Tasks;

namespace RVPark.Pages.Admin.Photos
{
    [Authorize(Roles = SD.AdminRole + "," + SD.StaffRole + "," + SD.CampHostRole)]
    public class CreateModel : PageModel
    {
        private readonly UnitOfWork _unitOfWork;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly string[] _allowedExtensions = { ".jpg", ".jpeg", ".png", ".gif" };
        private const int _maxFileSize = 5 * 1024 * 1024; // 5MB
        
        public int SiteId { get; set; }

        public Site SiteObj { get; set; }

        public CreateModel(UnitOfWork unitOfWork, IWebHostEnvironment webHostEnvironment)
        {
            _unitOfWork = unitOfWork;
            _webHostEnvironment = webHostEnvironment;
        }

        public IActionResult OnGet(int? siteId = null)
        {
            // Preselect site if siteId is provided
            if (siteId.HasValue)
            {
                Photo = new Photo { SiteId = siteId.Value };
                SiteId = siteId.Value;
                SiteObj = _unitOfWork.Site.GetById(SiteId);
            }

            ViewData["SiteId"] = new SelectList(_unitOfWork.Site.GetAll(), "SiteId", "Name");
            return Page();
        }

        [BindProperty]
        public Photo Photo { get; set; } = default!;

        public IActionResult OnPost()
        {
            string webRootPath = _webHostEnvironment.WebRootPath;
            var files = HttpContext.Request.Form.Files;

            // if the item is  new


            //was there an image submitted with the form
            if (files.Count > 0)
            {
                foreach ( var file in files)
                {
                    //Refresh new photo
                    Photo = new Photo { SiteId = Photo.SiteId };

                    //create a unique identifier for the image name.
                    string fileName = Guid.NewGuid().ToString();

                    //create variable to hold the path to the images/menuItems subfolder.
                    var uploads = Path.Combine(webRootPath, @"images\sitePhotos\");

                    //get and preserve the extention type
                    var extension = Path.GetExtension(file.FileName);

                    //create the complete full path string
                    var fullPath = uploads + fileName + extension;

                    using var fileStream = System.IO.File.Create(fullPath);
                    file.CopyTo(fileStream);
                    //associate the image to the MenuItem Object.

                    Photo.Name = @"\images\sitePhotos\" + fileName + extension;


                    //add the photo to the database
                    var currentPhoto = _unitOfWork.Photo.Get(p => p.SiteId == Photo.SiteId);
                    //if (currentPhoto != null)
                    //{
                    //    _unitOfWork.Photo.Delete(currentPhoto);
                    //}

                    _unitOfWork.Photo.Add(Photo);

                    _unitOfWork.Commit();
                }
                

                //redirect to menu items page
                return RedirectToPage("./Index", new { siteId = Photo.SiteId });
            }
            else
            {
                return Page();
            }
        }
    }
}

