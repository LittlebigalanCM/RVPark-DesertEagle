using ApplicationCore.Enums;
using ApplicationCore.Models;
using Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.Linq;

namespace RVPark.Pages.Admin.Fees
{
    public class DynamicFieldUpsertModel : PageModel
    {
        private readonly UnitOfWork _unitOfWork;

        public DynamicFieldUpsertModel(UnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        [BindProperty(SupportsGet = true)]
        public int? Id { get; set; }

        [BindProperty]
        public CustomDynamicField CustomField { get; set; }

        [BindProperty(SupportsGet = true)]
        public string ReturnUrl { get; set; }

        public IActionResult OnGet()
        {
            if (Id == null)
            {
                CustomField = new CustomDynamicField
                {
                    IsEnabled = true,
                    IsDeleted = false,
                    FieldType = DynamicFieldType.Text,
                    DisplayOrder = 0
                };
            }
            else
            {
                CustomField = _unitOfWork.CustomDynamicField.Get(f => f.Id == Id);
                if (CustomField == null)
                    return NotFound();

                // When editing dropdowns, make options appear one per line
                if (CustomField.FieldType == DynamicFieldType.Dropdown &&
                    !string.IsNullOrEmpty(CustomField.Note) &&
                    CustomField.Note.Contains(','))
                {
                    CustomField.Note = string.Join("\n", CustomField.Note.Split(',', StringSplitOptions.RemoveEmptyEntries));
                }
            }

            return Page();
        }

        public IActionResult OnPost()
        {
            ModelState.Remove("CustomField.FieldName");

            // --- Validation per Field Type ---
            switch (CustomField.FieldType)
            {
                case DynamicFieldType.Text:
                    if (string.IsNullOrWhiteSpace(CustomField.DisplayLabel))
                        ModelState.AddModelError("CustomField.DisplayLabel", "Display Label is required.");
                    break;

                case DynamicFieldType.TextInput:
                    if (string.IsNullOrWhiteSpace(CustomField.DisplayLabel))
                        ModelState.AddModelError("CustomField.DisplayLabel", "Display Label is required.");
                    if (string.IsNullOrWhiteSpace(CustomField.Note))
                        ModelState.AddModelError("CustomField.Note", "Help text or placeholder is required.");
                    break;

                case DynamicFieldType.Number:
                    if (CustomField.MinValue == null || CustomField.MaxValue == null)
                        ModelState.AddModelError("CustomField.MinValue", "Minimum and Maximum values are required.");
                    if (CustomField.MinValue > CustomField.MaxValue)
                        ModelState.AddModelError("CustomField.MinValue", "Minimum cannot exceed Maximum.");
                    break;

                case DynamicFieldType.Dropdown:
                    if (string.IsNullOrWhiteSpace(CustomField.Note))
                        ModelState.AddModelError("CustomField.Note", "You must enter at least one dropdown option.");
                    else
                    {
                        var options = CustomField.Note
                            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(o => o.Trim())
                            .Where(o => !string.IsNullOrEmpty(o))
                            .ToList();

                        if (!options.Any())
                            ModelState.AddModelError("CustomField.Note", "At least one valid dropdown option is required.");

                        // Store as comma-separated
                        CustomField.Note = string.Join(",", options);
                    }
                    break;

                case DynamicFieldType.Checkbox:
                    if (string.IsNullOrWhiteSpace(CustomField.Note))
                        ModelState.AddModelError("CustomField.Note", "Checkbox label text is required.");
                    break;

                case DynamicFieldType.Agreement:
                    if (string.IsNullOrWhiteSpace(CustomField.Note))
                        ModelState.AddModelError("CustomField.Note", "Agreement text is required.");

                    // Always provide default Agree/Disagree text if missing
                    if (string.IsNullOrWhiteSpace(CustomField.NoteAgreeText))
                        CustomField.NoteAgreeText = "Agree";
                    if (string.IsNullOrWhiteSpace(CustomField.NoteDisagreeText))
                        CustomField.NoteDisagreeText = "Disagree";

                    // Remove any lingering validation errors for these now-defaulted fields
                    ModelState.Remove("CustomField.NoteAgreeText");
                    ModelState.Remove("CustomField.NoteDisagreeText");
                    break;
            }

            // ✅ re-check validity after adjustments
            if (!ModelState.IsValid)
                return Page();

            // --- Normalize numeric fields ---
            if (CustomField.FieldType == DynamicFieldType.Number &&
                CustomField.MinValue > CustomField.MaxValue)
            {
                (CustomField.MinValue, CustomField.MaxValue) = (CustomField.MaxValue, CustomField.MinValue);
            }

            // --- Generate unique key (same as before) ---
            string baseKey = new string(CustomField.DisplayLabel
                .Where(char.IsLetterOrDigit)
                .ToArray())
                .ToLowerInvariant();

            string uniqueKey = baseKey;
            int counter = 1;

            var existingKeys = _unitOfWork.CustomDynamicField
                .GetAll(f => f.Id != CustomField.Id)
                .Select(f => f.FieldName)
                .ToHashSet();

            while (existingKeys.Contains(uniqueKey))
                uniqueKey = $"{baseKey}{counter++}";

            CustomField.FieldName = uniqueKey;

            // --- Save / Update as before ---
            if (CustomField.Id == 0)
            {
                // --- Handle Display Order Collisions ---
                var allFields = _unitOfWork.CustomDynamicField.GetAll(f => !f.IsDeleted).OrderBy(f => f.DisplayOrder).ToList();

                // Check if the new display order already exists in another field
                if (CustomField.Id == 0)
                {
                    // New field: shift all existing fields at or below the new display order
                    foreach (var field in allFields.Where(f => f.DisplayOrder >= CustomField.DisplayOrder))
                    {
                        field.DisplayOrder++;
                        _unitOfWork.CustomDynamicField.Update(field);
                    }
                }
                else
                {
                    // Editing existing field
                    var existing = allFields.FirstOrDefault(f => f.Id == CustomField.Id);
                    if (existing != null && existing.DisplayOrder != CustomField.DisplayOrder)
                    {
                        // If moved up (smaller number)
                        if (CustomField.DisplayOrder < existing.DisplayOrder)
                        {
                            foreach (var field in allFields
                                .Where(f => f.DisplayOrder >= CustomField.DisplayOrder && f.DisplayOrder < existing.DisplayOrder && f.Id != CustomField.Id))
                            {
                                field.DisplayOrder++;
                                _unitOfWork.CustomDynamicField.Update(field);
                            }
                        }
                        // If moved down (larger number)
                        else
                        {
                            foreach (var field in allFields
                                .Where(f => f.DisplayOrder <= CustomField.DisplayOrder && f.DisplayOrder > existing.DisplayOrder && f.Id != CustomField.Id))
                            {
                                field.DisplayOrder--;
                                _unitOfWork.CustomDynamicField.Update(field);
                            }
                        }
                    }
                }
                _unitOfWork.CustomDynamicField.Add(CustomField);
            }
            else
            {
                var original = _unitOfWork.CustomDynamicField.Get(f => f.Id == CustomField.Id);
                if (original == null)
                    return NotFound();

                original.DisplayLabel = CustomField.DisplayLabel;
                original.ConfirmationLabel = CustomField.ConfirmationLabel;
                original.DefaultValue = CustomField.DefaultValue;
                original.MinValue = CustomField.MinValue;
                original.MaxValue = CustomField.MaxValue;
                original.FieldType = CustomField.FieldType;
                original.DisplayOrder = CustomField.DisplayOrder;
                original.IsEnabled = CustomField.IsEnabled;
                original.Note = CustomField.Note;
                original.NoteTrigger = CustomField.NoteTrigger;
                original.ShowAgreeButtons = CustomField.ShowAgreeButtons;
                original.NoteAgreeText = CustomField.NoteAgreeText;
                original.NoteDisagreeText = CustomField.NoteDisagreeText;
                original.DisablePayOnDisagree = CustomField.DisablePayOnDisagree;
                original.IsDeleted = CustomField.IsDeleted;

                _unitOfWork.CustomDynamicField.Update(original);
            }

            _unitOfWork.Commit();

            if (!string.IsNullOrEmpty(ReturnUrl) && Url.IsLocalUrl(ReturnUrl))
                return LocalRedirect(ReturnUrl);

            return RedirectToPage("./ManageDynamicFields");
        }

    }
}
