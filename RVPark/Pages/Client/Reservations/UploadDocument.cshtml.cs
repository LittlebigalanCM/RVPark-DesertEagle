using System;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ApplicationCore.Models;
using Infrastructure.Data;
using Infrastructure.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

//I'll be honest: This is AI generated and I have no clue if it works.
//I just wanted something to possibly be able to handle fileuploads while I do work with confirming the reservations... 
//but I haven't been able to test it at all. Feel free to use this or ditch it

namespace RVPark.Pages.Client.Reservations
{
    [Authorize]
    public class UploadDocumentModel : PageModel
    {
        private readonly UnitOfWork _unitOfWork;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<UploadDocumentModel> _logger;

        public UploadDocumentModel(UnitOfWork unitOfWork, IWebHostEnvironment env, ILogger<UploadDocumentModel> logger)
        {
            _unitOfWork = unitOfWork;
            _env = env;
            _logger = logger;
        }

        [BindProperty(SupportsGet = true)]
        public int ReservationId { get; set; }

        [BindProperty]
        public IFormFile? DocumentFile { get; set; }

        [BindProperty(SupportsGet = true)]
        public string DocType { get; set; } = "PCS";

        public IActionResult OnGet()
        {
            var res = _unitOfWork.Reservation.Get(r => r.ReservationId == ReservationId);
            if (res == null) return NotFound();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!User.IsInRole(SD.AdminRole) && res.UserId != userId)
                return Forbid();

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var reservation = _unitOfWork.Reservation.Get(r => r.ReservationId == ReservationId);
            if (reservation == null) return NotFound();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!User.IsInRole(SD.AdminRole) && reservation.UserId != userId)
                return Forbid();

            if (DocumentFile == null || DocumentFile.Length == 0)
            {
                ModelState.AddModelError("DocumentFile", "Please select a file to upload.");
                return Page();
            }

            var allowedExt = new[] { ".pdf", ".jpg", ".jpeg", ".png" };
            var allowedMimes = new[] { "application/pdf", "image/jpeg", "image/png" };
            var ext = Path.GetExtension(DocumentFile.FileName).ToLowerInvariant();

            if (!allowedExt.Contains(ext) || !allowedMimes.Contains(DocumentFile.ContentType))
            {
                ModelState.AddModelError("DocumentFile", "Only PDF / JPG / PNG files are allowed.");
                return Page();
            }

            const long maxBytes = 10L * 1024 * 1024; // 10 MB
            if (DocumentFile.Length > maxBytes)
            {
                ModelState.AddModelError("DocumentFile", "File exceeds 10 MB limit.");
                return Page();
            }

            var uploadsDir = Path.Combine(_env.WebRootPath ?? "wwwroot", "uploads", "documents");
            Directory.CreateDirectory(uploadsDir);

            var safeFileName = $"{Guid.NewGuid()}{ext}";
            var physicalPath = Path.Combine(uploadsDir, safeFileName);

            try
            {
                await using var fs = new FileStream(physicalPath, FileMode.Create);
                await DocumentFile.CopyToAsync(fs);

                var doc = new Document
                {
                    Filepath = $"/uploads/documents/{safeFileName}",
                    DocType = string.IsNullOrWhiteSpace(DocType) ? "PCS" : DocType,
                    IsApproved = false,
                    ReservationId = ReservationId
                };

                _unitOfWork.Document.Add(doc);
                await _unitOfWork.CommitAsync();

                TempData["SuccessMessage"] = "File uploaded successfully.";
                return RedirectToPage("/Client/Reservations");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Upload failed for reservation {ReservationId}", ReservationId);
                ModelState.AddModelError(string.Empty, "Error saving file. Please try again.");
                return Page();
            }
        }
    }
}