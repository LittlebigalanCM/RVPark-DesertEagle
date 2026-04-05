using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Hosting;
using ApplicationCore.Interfaces;
using ApplicationCore.Models;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Infrastructure.Data;

namespace RVPark.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PhotoController : Controller
    {
        private readonly UnitOfWork _unitOfWork;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly string[] _allowedExtensions = { ".jpg", ".jpeg", ".png", ".gif" };
        private const int _maxFileSize = 5 * 1024 * 1024; // 5MB

        public PhotoController(UnitOfWork unitOfWork, IWebHostEnvironment webHostEnvironment)
        {
            _unitOfWork = unitOfWork;
            _webHostEnvironment = webHostEnvironment;
        }

        // GET: PhotoController/Details/5
        public ActionResult Details(int photoId)
        {
            var photo = _unitOfWork.Photo.Get(p => p.Id == photoId);

            if (photo == null)
            {
                return NotFound();
            }

            return View(photo);
        }

        // GET: PhotoController/Create
        public ActionResult Create()
        {
            // Load the sites for dropdown
            ViewBag.Sites = _unitOfWork.Site.GetAll();

            return View(new Photo());
        }

        // POST: PhotoController/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(IFormCollection collection)
        {
            try
            {
                // Get the uploaded file
                var file = Request.Form.Files.FirstOrDefault();
                if (file == null || file.Length == 0)
                {
                    ModelState.AddModelError("", "Please select a file to upload");
                    ViewBag.Sites = _unitOfWork.Site.GetAll();
                    return View(new Photo());
                }

                // Validate file size
                if (file.Length > _maxFileSize)
                {
                    ModelState.AddModelError("", $"File size exceeds the maximum limit of {_maxFileSize / 1024 / 1024}MB");
                    ViewBag.Sites = _unitOfWork.Site.GetAll();
                    return View(new Photo());
                }

                // Validate file extension
                string fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (!_allowedExtensions.Contains(fileExtension))
                {
                    ModelState.AddModelError("", $"Invalid file type. Allowed types: {string.Join(", ", _allowedExtensions)}");
                    ViewBag.Sites = _unitOfWork.Site.GetAll();
                    return View(new Photo());
                }

                // Generate a unique filename
                string uniqueFileName = $"{Guid.NewGuid()}{fileExtension}";

                // Create directories if they don't exist
                string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "photos");
                string thumbnailFolder = Path.Combine(uploadsFolder, "thumbnails");

                Directory.CreateDirectory(uploadsFolder);
                Directory.CreateDirectory(thumbnailFolder);

                // Save the full-size image
                string filePath = Path.Combine(uploadsFolder, uniqueFileName);
                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    file.CopyTo(fileStream);
                }

                // Create a simple copy as thumbnail (in a real application, you'd resize it)
                string thumbnailPath = Path.Combine(thumbnailFolder, uniqueFileName);
                System.IO.File.Copy(filePath, thumbnailPath);

                // Create the photo record in the database
                var photo = new Photo
                {
                    Name = uniqueFileName
                };

                // Handle SiteId if present
                if (int.TryParse(collection["SiteId"], out int siteId) && siteId > 0)
                {
                    photo.SiteId = siteId;
                }

                _unitOfWork.Photo.Add(photo);
                _unitOfWork.Commit();

                TempData["SuccessMessage"] = "Photo uploaded successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                // Log the exception
                ModelState.AddModelError("", $"An error occurred: {ex.Message}");
                ViewBag.Sites = _unitOfWork.Site.GetAll();
                return View(new Photo());
            }
        }

        // GET: PhotoController/Edit/5
        public ActionResult Edit(int id)
        {
            var photo = _unitOfWork.Photo.Get(p => p.Id == id);
            if (photo == null)
            {
                return NotFound();
            }

            ViewBag.Sites = _unitOfWork.Site.GetAll();
            return View(photo);
        }

        // POST: PhotoController/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(int id, IFormCollection collection)
        {
            try
            {
                var photo = _unitOfWork.Photo.Get(p => p.Id == id);
                if (photo == null)
                {
                    return NotFound();
                }
                
               

                // Check if a new image is being uploaded
                var file = Request.Form.Files.FirstOrDefault();
                if (file != null && file.Length > 0)
                {
                    // Validate file size
                    if (file.Length > _maxFileSize)
                    {
                        ModelState.AddModelError("", $"File size exceeds the maximum limit of {_maxFileSize / 1024 / 1024}MB");
                        ViewBag.Sites = _unitOfWork.Site.GetAll();
                        return View(photo);
                    }

                    // Validate file extension
                    string fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
                    if (!_allowedExtensions.Contains(fileExtension))
                    {
                        ModelState.AddModelError("", $"Invalid file type. Allowed types: {string.Join(", ", _allowedExtensions)}");
                        ViewBag.Sites = _unitOfWork.Site.GetAll();
                        return View(photo);
                    }

                    // Delete old files
                    string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "photos");
                    string thumbnailFolder = Path.Combine(uploadsFolder, "thumbnails");

                    string oldFilePath = Path.Combine(uploadsFolder, photo.Name);
                    string oldThumbnailPath = Path.Combine(thumbnailFolder, photo.Name);

                    if (System.IO.File.Exists(oldFilePath))
                    {
                        System.IO.File.Delete(oldFilePath);
                    }

                    if (System.IO.File.Exists(oldThumbnailPath))
                    {
                        System.IO.File.Delete(oldThumbnailPath);
                    }

                    // Save new file
                    string uniqueFileName = $"{Guid.NewGuid()}{fileExtension}";
                    string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        file.CopyTo(fileStream);
                    }

                    // Create new thumbnail
                    string thumbnailPath = Path.Combine(thumbnailFolder, uniqueFileName);
                    System.IO.File.Copy(filePath, thumbnailPath);

                    // Update the file name
                    photo.Name = uniqueFileName;
                }

                _unitOfWork.Photo.Update(photo);
                _unitOfWork.Commit();

                TempData["SuccessMessage"] = "Photo updated successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"An error occurred: {ex.Message}");
                var photo = _unitOfWork.Photo.Get(p => p.Id == id);
                ViewBag.Sites = _unitOfWork.Site.GetAll();
                return View(photo);
            }
        }

        // POST: PhotoController/Delete/5
        [HttpDelete("{id}")]
        public IActionResult Delete(int id)
        {
            try
            {
                var photo = _unitOfWork.Photo.Get(p => p.Id == id);

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

                // Delete database record
                _unitOfWork.Photo.Delete(photo);
                _unitOfWork.Commit();

                TempData["SuccessMessage"] = "Photo deleted successfully!";
                return Json(new { success = true, message = "Delete Succesful" });
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Failed to delete photo: {ex.Message}";
                return Json(new { success = false, message = "Error while deleting" });
            }
        }
    }
}
