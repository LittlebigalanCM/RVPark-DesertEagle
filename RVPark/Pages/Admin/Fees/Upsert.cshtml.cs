using ApplicationCore.Dtos;
using ApplicationCore.Enums;
using ApplicationCore.Models;
using Infrastructure.Data;
using Infrastructure.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace RVPark.Pages.Admin.Fees
{
    [Authorize(Roles = SD.AdminRole)]
    public class UpsertModel : PageModel
    {
        private readonly UnitOfWork _unitOfWork;

        public UpsertModel(UnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        [BindProperty]
        public Fee FeeObject { get; set; }

        public SelectList TriggerTypeList { get; set; }
        public SelectList CalculationTypeList { get; set; }
        public SelectList CustomFieldSelectList { get; set; }
        public List<CustomDynamicField> CustomFields { get; set; }

        [BindProperty(SupportsGet = true)]
        public string ReturnUrl { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? FeeId { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            // Only get field types that support automatic fee rules: Number, Dropdown, Checkbox
            CustomFields = _unitOfWork.CustomDynamicField
                .GetAll(f => !f.IsDeleted && f.IsEnabled &&
                    (f.FieldType == DynamicFieldType.Number ||
                     f.FieldType == DynamicFieldType.Dropdown ||
                     f.FieldType == DynamicFieldType.Checkbox))
                .OrderBy(f => f.DisplayOrder)
                .ToList();

            var triggerTypes = Enum.GetValues(typeof(TriggerType)).Cast<TriggerType>();
            var calcTypes = Enum.GetValues(typeof(CalculationType)).Cast<CalculationType>();

            TriggerTypeList = new SelectList(triggerTypes);
            CalculationTypeList = new SelectList(calcTypes);
            CustomFieldSelectList = new SelectList(CustomFields, "Id", "DisplayLabel");

            if (FeeId == null)
            {
                FeeObject = new Fee
                {
                    TriggerType = TriggerType.Manual,
                    CalculationType = CalculationType.StaticAmount,
                    IsEnabled = true
                };
            }
            else
            {
                FeeObject = await _unitOfWork.Fee.GetFirstOrDefaultAsync(f => f.FeeId == FeeId);
                if (FeeObject == null)
                {
                    return NotFound();
                }

                if (FeeObject.TriggerType == TriggerType.Automatic && !string.IsNullOrEmpty(FeeObject.TriggerRuleJson))
                {
                    try
                    {
                        var rule = System.Text.Json.JsonSerializer.Deserialize<AutomaticRuleDto>(
                            FeeObject.TriggerRuleJson,
                            new System.Text.Json.JsonSerializerOptions
                            {
                                PropertyNameCaseInsensitive = true
                            });
                        ViewData["ExistingRule"] = rule;
                    }
                    catch
                    {
                        // Ignore Errors
                    }
                }
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                // Only get field types that support automatic fee rules: Number, Dropdown, Checkbox
                CustomFields = _unitOfWork.CustomDynamicField
                    .GetAll(f => !f.IsDeleted && f.IsEnabled &&
                        (f.FieldType == DynamicFieldType.Number ||
                         f.FieldType == DynamicFieldType.Dropdown ||
                         f.FieldType == DynamicFieldType.Checkbox))
                    .OrderBy(f => f.DisplayOrder)
                    .ToList();

                var triggerTypes = Enum.GetValues(typeof(TriggerType)).Cast<TriggerType>();
                var calcTypes = Enum.GetValues(typeof(CalculationType)).Cast<CalculationType>();

                TriggerTypeList = new SelectList(triggerTypes);
                CalculationTypeList = new SelectList(calcTypes);
                CustomFieldSelectList = new SelectList(CustomFields, "Id", "DisplayLabel");

                return Page();
            }

            if (FeeObject.FeeId == 0)
                await _unitOfWork.Fee.AddAsync(FeeObject);
            else
                _unitOfWork.Fee.Update(FeeObject);

            await _unitOfWork.CommitAsync();

            return string.IsNullOrWhiteSpace(ReturnUrl)
                ? RedirectToPage("./Index")
                : Redirect(ReturnUrl);
        }
    }
}
