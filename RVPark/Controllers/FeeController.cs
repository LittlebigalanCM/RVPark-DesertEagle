using ApplicationCore.Enums;
using Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace RVPark.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FeeController : Controller
    {
        // UnitOfWork provides access to repositories and handles commits
        private readonly UnitOfWork _unitOfWork;

        public FeeController(UnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        [HttpGet]
        public IActionResult Get()
        {
            // Load all fees from the database
            var fees = _unitOfWork.Fee.GetAllQueryable().ToList();

            // Transform fee data into a format ready for the frontend
            var data = fees.Select(fee =>
            {
                // Base calculation type (fallback)
                string calc = fee.CalculationType?.ToString() ?? "StaticAmount";
                string amount = "$0.00";

                // Variables to store rule-level information if present
                decimal? ruleStatic = null;
                decimal? rulePercent = null;
                string? ruleCalc = null;

                // Parse JSON trigger rule only when applicable (Automatic triggers only)
                if (!string.IsNullOrWhiteSpace(fee.TriggerRuleJson) && fee.TriggerType == TriggerType.Automatic)
                {
                    try
                    {
                        // Parse the JSON safely
                        using var doc = System.Text.Json.JsonDocument.Parse(fee.TriggerRuleJson);
                        var root = doc.RootElement;

                        // Handle either array-based or object-based rule formats
                        var rule = root.ValueKind == System.Text.Json.JsonValueKind.Array && root.GetArrayLength() > 0
                            ? root[0]
                            : root.ValueKind == System.Text.Json.JsonValueKind.Object ? root : default;

                        // Extract rule-level calculation values
                        if (rule.ValueKind != System.Text.Json.JsonValueKind.Undefined)
                        {
                            if (rule.TryGetProperty("CalculationType", out var rCalc) && rCalc.ValueKind == System.Text.Json.JsonValueKind.String)
                                ruleCalc = rCalc.GetString();

                            if (rule.TryGetProperty("StaticAmount", out var rSta) && rSta.ValueKind == System.Text.Json.JsonValueKind.Number)
                                ruleStatic = rSta.GetDecimal();

                            if (rule.TryGetProperty("Percentage", out var rPct) && rPct.ValueKind == System.Text.Json.JsonValueKind.Number)
                                rulePercent = rPct.GetDecimal();
                        }
                    }
                    catch
                    {
                        // Ignore JSON parsing errors silently — invalid rule formats won't break API
                    }
                }

                // Override base calculation type if rule defined one
                calc = ruleCalc ?? calc;

                // Determine the correct amount display
                if (ruleStatic.HasValue)
                    amount = ruleStatic.Value.ToString("C");
                else if (rulePercent.HasValue)
                    amount = $"{rulePercent.Value:0.##}%";
                else if (fee.CalculationType == CalculationType.PercentOfTotal && fee.Percentage.HasValue)
                    amount = $"{fee.Percentage.Value:0.##}%";
                else
                    amount = (fee.StaticAmount ?? 0m).ToString("C");

                // Shape the final response object
                return new
                {
                    fee.FeeId,
                    fee.Name,
                    amountDisplay = amount,
                    calculationTypeDisplay = GetDisplayName(fee.CalculationType ?? CalculationType.StaticAmount),
                    triggerTypeDisplay = fee.TriggerType.ToString(),
                    isEnabled = fee.IsEnabled
                };
            });

            return Json(new { data });
        }

        [HttpGet("GetStaticAmount")]
        public IActionResult GetStaticAmount(int feeId)
        {
            // Retrieve fee by ID
            var fee = _unitOfWork.Fee.Get(f => f.FeeId == feeId);
            if (fee == null)
                return Json(new { success = false, message = "Fee not found." });

            return Json(new { amount = fee.StaticAmount });
        }

        [HttpGet("GetStaticAmountByName")]
        public IActionResult GetStaticAmountByName(string feeName)
        {
            // Retrieve fee by name
            var fee = _unitOfWork.Fee.Get(f => f.Name == feeName);
            if (fee == null)
                return Json(new { success = false, message = "Fee not found." });

            return Json(new { amount = fee.StaticAmount });
        }

        [HttpGet("GetEnabledStatus")]
        public IActionResult GetEnabledStatus(int feeId)
        {
            // Retrieve fee to check status
            var fee = _unitOfWork.Fee.Get(f => f.FeeId == feeId);
            if (fee == null)
                return Json(new { success = false, message = "No fee found with given ID." });

            return Json(new { isEnabled = fee.IsEnabled });
        }

        [HttpPost("ToggleFeeStatus")]
        public IActionResult ToggleFeeStatus(int feeId)
        {
            // Retrieve fee to update status
            var fee = _unitOfWork.Fee.Get(f => f.FeeId == feeId);
            if (fee == null)
                return Json(new { success = false, message = "No fee found with given ID." });

            // Flip enabled state
            fee.IsEnabled = !fee.IsEnabled;
            _unitOfWork.Fee.Update(fee);
            _unitOfWork.Commit();

            return Json(new
            {
                success = true,
                message = fee.IsEnabled
                    ? "Fee has been enabled successfully."
                    : "Fee has been disabled successfully."
            });
        }

        // Helper: Gets [Display(Name="...")] attribute for enums
        private static string GetDisplayName(Enum value)
        {
            var field = value.GetType().GetField(value.ToString());
            var attribute = field?.GetCustomAttributes(typeof(DisplayAttribute), false).Cast<DisplayAttribute>().FirstOrDefault();
            return attribute?.Name ?? value.ToString();
        }
    }
}
