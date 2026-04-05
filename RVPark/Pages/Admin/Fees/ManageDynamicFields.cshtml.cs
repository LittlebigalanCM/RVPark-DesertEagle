using Infrastructure.Data;
using Infrastructure.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ApplicationCore.Models;

namespace RVPark.Pages.Admin.Fees
{
    [Authorize(Roles = SD.AdminRole)]
    public class ManageDynamicFieldsModel : PageModel
    {
        private readonly UnitOfWork _unitOfWork;

        public ManageDynamicFieldsModel(UnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork; // Inject UnitOfWork for DB operations
        }

        public List<CustomDynamicField> Fields { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public string? ReturnUrl { get; set; } // Optional return URL for navigation

        public void OnGet()
        {
            // Load all active (non-deleted) dynamic fields, ordered by display order then label
            Fields = _unitOfWork.CustomDynamicField
                .GetAll(f => !f.IsDeleted)
                .OrderBy(f => f.DisplayOrder)
                .ThenBy(f => f.DisplayLabel)
                .ToList();
        }

        public IActionResult OnPostUpdateStatus(int id, string actionType)
        {
            // Validate input
            if (id <= 0 || string.IsNullOrWhiteSpace(actionType))
            {
                TempData["Error"] = "Invalid request.";
                return RedirectToPage();
            }

            // Retrieve the field by ID, ensuring it isn't deleted
            var field = _unitOfWork.CustomDynamicField.Get(f => f.Id == id && !f.IsDeleted);
            if (field == null)
            {
                TempData["Error"] = "Field not found.";
                return RedirectToPage();
            }

            // Determine which action to perform
            switch (actionType.ToLowerInvariant())
            {
                case "toggle":
                    // Toggle enabled/disabled state
                    field.IsEnabled = !field.IsEnabled;
                    _unitOfWork.CustomDynamicField.Update(field);
                    _unitOfWork.Commit();

                    TempData["Success"] = $"Field '{field.DisplayLabel}' has been {(field.IsEnabled ? "enabled" : "disabled")}.";
                    break;

                case "delete":
                    // Soft-delete the field
                    field.IsDeleted = true;
                    _unitOfWork.CustomDynamicField.Update(field);
                    _unitOfWork.Commit();

                    TempData["Success"] = $"Field '{field.DisplayLabel}' deleted successfully.";
                    break;

                default:
                    // Unknown action received
                    TempData["Error"] = "Unknown action type.";
                    break;
            }

            return RedirectToPage();
        }

        public IActionResult OnPostReorder(string orderedIds)
        {
            // Ensure client sent ordering data
            if (string.IsNullOrWhiteSpace(orderedIds))
            {
                TempData["Error"] = "No order data was received.";
                return RedirectToPage();
            }

            // Parse CSV list of IDs into integers
            var idList = orderedIds.Split(',')
                .Select(id => int.TryParse(id, out var parsed) ? parsed : (int?)null)
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .ToList();

            if (!idList.Any())
            {
                TempData["Error"] = "Invalid order data.";
                return RedirectToPage();
            }

            // Retrieve all fields that match the provided IDs and are not deleted
            var allFields = _unitOfWork.CustomDynamicField
                .GetAll(f => idList.Contains(f.Id) && !f.IsDeleted)
                .ToList();

            // Apply the new order by iterating through the ID list in order
            int order = 0;
            foreach (var id in idList)
            {
                var field = allFields.FirstOrDefault(f => f.Id == id);
                if (field != null)
                {
                    field.DisplayOrder = order++;
                    _unitOfWork.CustomDynamicField.Update(field);
                }
            }

            // Commit all updates to the database
            _unitOfWork.Commit();
            TempData["Success"] = "Display order updated successfully.";

            return RedirectToPage();
        }
    }
}
