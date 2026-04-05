using Infrastructure.Data;
using Infrastructure.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace RVPark.Controllers
{
    [Route("CustomDynamicField")]
    [Authorize(Roles = SD.AdminRole)]
    public class CustomDynamicFieldController : Controller
    {
        private readonly UnitOfWork _unitOfWork;

        public CustomDynamicFieldController(UnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        [HttpPost("UpdateFieldNote")]
        public IActionResult UpdateFieldNote(
        string fieldName,
        string confirationLabel,
        string note,
        string noteTrigger,
        string defaultValue,
        string minValue,
        string maxValue,
        string noteAgreeText,
        string noteDisagreeText,
        string agreeRedirectUrl,
        string disagreeRedirectUrl,
        bool disablePayOnDisagree)
        {
            if (string.IsNullOrWhiteSpace(fieldName))
                return BadRequest("Field name is required.");

            var field = _unitOfWork.CustomDynamicField.Get(f => f.FieldName == fieldName && !f.IsDeleted);
            if (field == null)
                return NotFound($"No custom field found for name '{fieldName}'.");

            field.Note = note?.Trim();
            field.NoteTrigger = noteTrigger;
            field.NoteAgreeText = noteAgreeText;
            field.NoteDisagreeText = noteDisagreeText;
            field.AgreeRedirectUrl = string.IsNullOrWhiteSpace(agreeRedirectUrl) ? null : agreeRedirectUrl;
            field.DisagreeRedirectUrl = string.IsNullOrWhiteSpace(disagreeRedirectUrl) ? null : disagreeRedirectUrl;
            field.DisablePayOnDisagree = disablePayOnDisagree;

            field.DefaultValue = int.TryParse(defaultValue, out var defVal) ? defVal : null;
            field.MaxValue = int.TryParse(maxValue, out var maxVal) ? maxVal : null;

            _unitOfWork.CustomDynamicField.Update(field);
            _unitOfWork.Commit();

            return Ok(new { success = true });
        }

    }
}
