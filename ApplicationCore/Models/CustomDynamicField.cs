using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using ApplicationCore.Enums;

public class CustomDynamicField : IValidatableObject
{
    public int Id { get; set; }

    public string FieldName { get; set; }

    [Required(ErrorMessage = "Display Label is required.")]
    [Display(Name = "Display Label")]
    public string DisplayLabel { get; set; }

    [Display(Name = "Field Type")]
    public DynamicFieldType FieldType { get; set; } = DynamicFieldType.Text;

    // ----- Ordering -----
    [Display(Name = "Display Order")]
    public int DisplayOrder { get; set; } = 0;

    // ----- Number-only -----
    [Display(Name = "Default Number")]
    public int? DefaultValue { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "Minimum must be 0 or greater.")]
    public int? MinValue { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "Maximum must be 0 or greater.")]
    public int? MaxValue { get; set; }

    // ----- Dropdown-only -----
    public string? OptionsJson { get; set; }

    [Display(Name = "Default Text")]
    public string? StringDefaultValue { get; set; }

    // ----- TextInput-only -----
    [Display(Name = "Placeholder")]
    [MaxLength(255)]
    public string? Placeholder { get; set; }

    [Display(Name = "Max Length")]
    [Range(1, 10000)]
    public int? MaxLength { get; set; }

    [Display(Name = "Multiline")]
    public bool IsMultiline { get; set; } = false;

    [Display(Name = "Required")]
    public bool IsRequired { get; set; } = false;

    public string? Note { get; set; } = string.Empty;

    public string? NoteTrigger { get; set; }

    // ----- Flags -----
    [Display(Name = "Enabled")]
    public bool IsEnabled { get; set; } = true;

    [Display(Name = "Trigger Field")]
    public bool IsTriggerField { get; set; } = false;

    [Display(Name = "Confirmation Label")]
    public string? ConfirmationLabel { get; set; }

    public bool IsDeleted { get; set; } = false;

    // ----- Agreement-only -----
    public string? NoteAgreeText { get; set; }
    public string? NoteDisagreeText { get; set; }
    public string? AgreeRedirectUrl { get; set; }
    public string? DisagreeRedirectUrl { get; set; }
    public bool ShowAgreeButtons { get; set; } = false;
    public bool DisablePayOnDisagree { get; set; } = false;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // DisplayLabel and FieldName checks
        if (string.IsNullOrWhiteSpace(DisplayLabel))
            yield return new ValidationResult("Display Label is required.", new[] { nameof(DisplayLabel) });

        if (string.IsNullOrWhiteSpace(FieldName))
            yield return new ValidationResult("Field Name is required.", new[] { nameof(FieldName) });

        switch (FieldType)
        {
            case DynamicFieldType.Text:
                if (MaxLength.HasValue && MaxLength <= 0)
                    yield return new ValidationResult("Max Length must be greater than 0.", new[] { nameof(MaxLength) });

                if (!string.IsNullOrEmpty(StringDefaultValue) && MaxLength.HasValue && StringDefaultValue.Length > MaxLength)
                    yield return new ValidationResult("Default text exceeds Max Length.", new[] { nameof(StringDefaultValue) });
                break;

            case DynamicFieldType.Number:
                if (MinValue.HasValue && MaxValue.HasValue && MinValue > MaxValue)
                    yield return new ValidationResult("Minimum cannot be greater than Maximum.", new[] { nameof(MinValue), nameof(MaxValue) });

                if (DefaultValue.HasValue)
                {
                    if (MinValue.HasValue && DefaultValue < MinValue)
                        yield return new ValidationResult("Default value is below Minimum.", new[] { nameof(DefaultValue) });
                    if (MaxValue.HasValue && DefaultValue > MaxValue)
                        yield return new ValidationResult("Default value is above Maximum.", new[] { nameof(DefaultValue) });
                }
                break;

            case DynamicFieldType.Dropdown:
                if (string.IsNullOrWhiteSpace(OptionsJson))
                {
                    yield return new ValidationResult("Options are required for a dropdown.", new[] { nameof(OptionsJson) });
                    break;
                }

                List<string>? options = null;
                bool parseFailed = false;
                try
                {
                    options = JsonSerializer.Deserialize<List<string>>(OptionsJson);
                }
                catch
                {
                    parseFailed = true;
                }
                if (parseFailed)
                {
                    yield return new ValidationResult("OptionsJson must be a valid JSON array of strings.", new[] { nameof(OptionsJson) });
                    yield break;
                }

                if (options == null || options.Count == 0)
                    yield return new ValidationResult("Provide at least one dropdown option.", new[] { nameof(OptionsJson) });

                if (!string.IsNullOrEmpty(StringDefaultValue) && options != null && !options.Contains(StringDefaultValue))
                    yield return new ValidationResult("Default value must be one of the dropdown options.", new[] { nameof(StringDefaultValue) });
                break;

            case DynamicFieldType.Agreement:
                if (string.IsNullOrWhiteSpace(Note))
                    yield return new ValidationResult("Note text is required for Agreement fields.", new[] { nameof(Note) });
                break;
        }

        if (IsTriggerField && string.IsNullOrWhiteSpace(NoteTrigger))
            yield return new ValidationResult("A trigger text/expression is required when IsTriggerField is enabled.", new[] { nameof(NoteTrigger) });
    }
}
