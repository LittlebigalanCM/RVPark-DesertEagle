using ApplicationCore.Dtos;
using ApplicationCore.Models;
using System.Text.Json;

namespace Infrastructure.Services
{
    public static class TriggerRuleEvaluator
    {
        public static bool Evaluate(string json, Reservation reservation, string? transactionTypeNameOrLabel = null)
        {
            if (string.IsNullOrWhiteSpace(json)) return false;

            try
            {
                var rule = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;

                if (!rule.TryGetValue("Field", out var f) ||
                    !rule.TryGetValue("Operator", out var o) ||
                    !rule.TryGetValue("Value", out var v))
                    return false;

                string? fieldName = f.GetString()?.Trim();
                string? op = o.GetString()?.Trim();
                object? valueRaw = ExtractValue(v);

                // Get field type if present (for new-style rules)
                string? fieldType = rule.TryGetValue("FieldType", out var ft) ? ft.GetString() : null;

                if (string.IsNullOrWhiteSpace(fieldName) || string.IsNullOrWhiteSpace(op))
                    return false;

                object? actualValue = null;

                // Handle special HoursBefore rule
                if (fieldName.Equals("HoursBefore", StringComparison.OrdinalIgnoreCase) &&
                    transactionTypeNameOrLabel?.ToLower().Contains("cancel") == true)
                {
                    actualValue = (int)Math.Round((reservation.StartDate - DateTime.UtcNow).TotalHours);
                }
                else if (reservation.GetType().GetProperty(fieldName) is var prop && prop != null)
                {
                    actualValue = prop.GetValue(reservation);
                }
                else if (reservation.DynamicData != null &&
                         reservation.DynamicData.TryGetValue(fieldName, out var dynVal))
                {
                    actualValue = dynVal;
                }

                if (actualValue == null || valueRaw == null)
                    return false;

                // Handle boolean comparisons (Checkbox fields)
                if (fieldType == "Checkbox" || valueRaw is bool)
                {
                    bool targetBool = valueRaw is bool b ? b :
                        (valueRaw.ToString()?.Equals("true", StringComparison.OrdinalIgnoreCase) ?? false);

                    bool actualBool = actualValue is bool ab ? ab :
                        (actualValue.ToString()?.Equals("true", StringComparison.OrdinalIgnoreCase) ?? false);

                    return op switch
                    {
                        "==" => actualBool == targetBool,
                        "!=" => actualBool != targetBool,
                        _ => false
                    };
                }

                // Handle numeric comparisons (Number fields)
                if (decimal.TryParse(actualValue.ToString(), out var actualNum) &&
                    decimal.TryParse(valueRaw.ToString(), out var targetNum))
                {
                    return op switch
                    {
                        ">" => actualNum > targetNum,
                        ">=" => actualNum >= targetNum,
                        "<" => actualNum < targetNum,
                        "<=" => actualNum <= targetNum,
                        "==" => actualNum == targetNum,
                        "!=" => actualNum != targetNum,
                        _ => false
                    };
                }

                // Handle string comparisons (Dropdown fields and fallback)
                var actualStr = actualValue.ToString()?.Trim('"').Trim();
                var targetStr = valueRaw.ToString()?.Trim('"').Trim();

                return op switch
                {
                    "==" => string.Equals(actualStr, targetStr, StringComparison.OrdinalIgnoreCase),
                    "!=" => !string.Equals(actualStr, targetStr, StringComparison.OrdinalIgnoreCase),
                    _ => false
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ TriggerRuleEvaluator.Evaluate failed: {ex.Message}");
                return false;
            }
        }

        public static int EvaluateUnits(string json, Reservation reservation, string? transactionTypeNameOrLabel = null)
        {
            try
            {
                var rule = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
                if (rule == null || !rule.TryGetValue("Field", out var fieldElement))
                    return 0;

                string? fieldName = fieldElement.GetString()?.Trim();
                if (string.IsNullOrWhiteSpace(fieldName))
                    return 0;

                // Get field type if present
                string? fieldType = rule.TryGetValue("FieldType", out var ft) ? ft.GetString() : null;

                // For Dropdown and Checkbox fields, just check if rule matches (1 or 0)
                if (fieldType == "Dropdown" || fieldType == "Checkbox")
                {
                    bool matches = Evaluate(json, reservation, transactionTypeNameOrLabel);
                    Console.WriteLine($"␦ Field: {fieldName} (Type: {fieldType}), Rule matches: {matches} → {(matches ? 1 : 0)} unit(s)");
                    return matches ? 1 : 0;
                }

                // Handle special HoursBefore rule for cancellations
                if (fieldName.Equals("HoursBefore", StringComparison.OrdinalIgnoreCase) &&
                    transactionTypeNameOrLabel?.ToLower().Contains("cancel") == true)
                {
                    int hoursValue = (int)Math.Round((reservation.StartDate - DateTime.UtcNow).TotalHours);

                    if (Evaluate(json, reservation, transactionTypeNameOrLabel))
                    {
                        Console.WriteLine($"␦ HoursBefore={hoursValue}, rule met → 1 unit");
                        return 1;
                    }
                    else
                    {
                        Console.WriteLine($"␦ HoursBefore={hoursValue}, rule not met → 0 units");
                        return 0;
                    }
                }

                // For Number fields, calculate units based on value exceeding threshold
                int actualValue = 0;
                if (reservation.DynamicData != null &&
                    reservation.DynamicData.TryGetValue(fieldName, out var rawVal) &&
                    int.TryParse(rawVal?.ToString(), out int parsedVal))
                {
                    actualValue = parsedVal;
                }
                else
                {
                    Console.WriteLine($"DynamicData missing field '{fieldName}'");
                    return 0;
                }

                // Get the threshold value from the rule
                int baseIncluded = 0;
                if (rule.TryGetValue("Value", out var valueElement))
                {
                    if (valueElement.ValueKind == JsonValueKind.Number)
                    {
                        baseIncluded = valueElement.GetInt32();
                    }
                    else if (int.TryParse(valueElement.ToString(), out var parsed))
                    {
                        baseIncluded = parsed;
                    }
                }

                // Get operator to determine how to calculate units
                string? op = rule.TryGetValue("Operator", out var opElement) ? opElement.GetString() : ">";

                int units = 0;
                switch (op)
                {
                    case ">":
                        // Units = how many over the threshold (e.g., 5 adults with threshold 4 = 1 extra)
                        units = Math.Max(0, actualValue - baseIncluded);
                        break;
                    case ">=":
                        // Units = how many at or over the threshold
                        units = actualValue >= baseIncluded ? Math.Max(0, actualValue - baseIncluded + 1) : 0;
                        break;
                    case "==":
                    case "!=":
                    case "<":
                    case "<=":
                        // For these operators, just check if condition is met (1 or 0)
                        units = Evaluate(json, reservation, transactionTypeNameOrLabel) ? 1 : 0;
                        break;
                    default:
                        units = Math.Max(0, actualValue - baseIncluded);
                        break;
                }

                Console.WriteLine($"␦ Field: {fieldName}, Actual: {actualValue}, Base: {baseIncluded}, Op: {op}, Units: {units}");
                return units;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ TriggerRuleEvaluator.EvaluateUnits failed: {ex.Message}");
                return 0;
            }
        }

        private static object? ExtractValue(JsonElement v)
        {
            return v.ValueKind switch
            {
                JsonValueKind.String => v.GetString(),
                JsonValueKind.Number => v.TryGetDecimal(out var d) ? d : null,
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => v.GetRawText()
            };
        }
    }
}
