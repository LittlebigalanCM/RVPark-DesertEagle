using ApplicationCore.Models;
using System.Text;
using System.Text.Json;

namespace Infrastructure.Services
{
    public class TriggerBreakdownService
    {
        private readonly List<CustomDynamicField> _fields;

        public TriggerBreakdownService(List<CustomDynamicField> dynamicFields)
        {
            _fields = dynamicFields;
        }

        public string ToHumanReadable(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return string.Empty;

            try
            {
                var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                var sb = new StringBuilder();

                if (root.ValueKind == JsonValueKind.Object)
                {
                    AppendFriendlyRule(root, sb);
                }
                else if (root.ValueKind == JsonValueKind.Array)
                {
                    foreach (var condition in root.EnumerateArray())
                    {
                        AppendFriendlyRule(condition, sb);
                    }
                }

                return sb.ToString();
            }
            catch
            {
                return "<em>Unable to display rule breakdown (invalid format).</em>";
            }
        }
        private void AppendFriendlyRule(JsonElement rule, StringBuilder sb)
        {
            string? field = rule.TryGetProperty("Field", out var f) ? f.GetString() : null;
            string? op = rule.TryGetProperty("Operator", out var o) ? o.GetString() : null;
            string? valRaw = rule.TryGetProperty("Value", out var v) ? v.ToString() : null;
            string? baseIncludedRaw = rule.TryGetProperty("BaseIncluded", out var b) ? b.ToString() : null;
            string? calcType = rule.TryGetProperty("CalculationType", out var c) ? c.GetString() : null;
            string? amountStr = rule.TryGetProperty("Amount", out var a) ? a.ToString() : null;
            bool isDailyRate = rule.TryGetProperty("IsDailyRate", out var pn) && pn.GetBoolean();

            string displayLabel = GetFieldLabel(field);
            string opText = TranslateOperator(op);

            // Explain why the fee was triggered
            sb.Append($"<div class='mb-2'><strong>•</strong> This fee was added because there were {opText} {valRaw} <strong>{displayLabel}</strong>.</div>");

            // Attempt parsing with fallbacks
            bool amountParsed = decimal.TryParse(amountStr, out decimal amount);
            bool baseParsed = int.TryParse(baseIncludedRaw, out int baseIncluded);
            bool valParsed = int.TryParse(valRaw, out int ruleValue);

            sb.Append("<div class='text-muted small fst-italic ms-4 mt-1'>");

            if (!amountParsed)
            {
                sb.Append("Cannot calculate breakdown: missing or invalid <strong>Amount</strong>.");
            }
            else if (!valParsed)
            {
                sb.Append("Cannot calculate breakdown: <strong>Value</strong> is not a number.");
            }
            else
            {
                // Default baseIncluded to 0 if not provided
                if (!baseParsed) baseIncluded = 0;

                int overUnits = Math.Max(0, ruleValue - baseIncluded);
                int nights = EstimateNights();
                decimal total = isDailyRate ? nights * overUnits * amount : overUnits * amount;

                if (overUnits <= 0)
                {
                    sb.Append($"No overage applied — {ruleValue} {displayLabel.ToLower()} is within allowed limit of {baseIncluded}.");
                }
                else if (isDailyRate)
                {
                    sb.Append($"{nights} nights × {overUnits} extra × ${amount:0.00} = <strong>${total:0.00}</strong>");
                }
                else
                {
                    sb.Append($"{overUnits} extra × ${amount:0.00} = <strong>${total:0.00}</strong>");
                }
            }

            sb.Append("</div>");
        }

        private string GetFieldLabel(string? fieldKey)
        {
            if (string.IsNullOrWhiteSpace(fieldKey))
                return "unknown field";

            string normalizedKey = fieldKey.Trim().TrimStart('[').TrimEnd(']');

            var match = _fields.FirstOrDefault(f =>
                string.Equals(f.FieldName?.Trim(), normalizedKey, StringComparison.OrdinalIgnoreCase));

            return match?.DisplayLabel;
        }



        private string TranslateOperator(string? op) => op switch
        {
            ">" => "more than",
            "<" => "less than",
            ">=" => "at least",
            "<=" => "at most",
            "==" => "exactly",
            "!=" => "not equal to",
            _ => op ?? "matching"
        };

        private int EstimateNights()
        {
            var nightsField = _fields.FirstOrDefault(f => f.FieldName.Equals("NumberOfNights", StringComparison.OrdinalIgnoreCase));
            return nightsField?.DefaultValue ?? 1;
        }
    }
}
