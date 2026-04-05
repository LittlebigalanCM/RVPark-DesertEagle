using ApplicationCore.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ApplicationCore.Models
{
    public class Fee
    {
        [Key]
        public int FeeId { get; set; }

        [Required]
        [Display(Name = "Fee Name")]
        public string? Name { get; set; }

        [Display(Name = "Display Label")]
        public string? DisplayLabel { get; set; }
        [Required]
        public TriggerType TriggerType { get; set; }

        [Display(Name = "Trigger Rule JSON")]
        public string? TriggerRuleJson { get; set; }

        [Display(Name = "Trigger Template Type")]
        public string? TriggerTemplateType { get; set; }

        [Display(Name = "Calculation Type")]
        public CalculationType? CalculationType { get; set; }

        [Display(Name = "Static Amount")]
        [Range(0, double.MaxValue, ErrorMessage = "Static Amount must be non-negative")]
        public decimal? StaticAmount { get; set; }

        [Display(Name = "Percentage")]
        [Range(0,1000, ErrorMessage = "Percentage must be between 0 and 1000")]
        public decimal? Percentage { get; set; }

        [Display(Name = "Related Additional Field")]
        public int? CustomDynamicFieldId { get; set; }

        [ForeignKey(nameof(CustomDynamicFieldId))]
        public CustomDynamicField? RelatedCustomDynamicField { get; set; }

        public bool IsEnabled { get; set; } = true;
        public decimal GetEffectiveValue(decimal reservationTotal, int numberOfNights = 0, int units = 0)
        {
            var type = CalculationType ?? Enums.CalculationType.StaticAmount;

            return type switch
            { Enums.CalculationType.StaticAmount => Math.Round(StaticAmount ?? 0m, 2),
              Enums.CalculationType.PercentOfTotal => Percentage.HasValue ? Math.Round((Percentage.Value / 100m) * reservationTotal, 2): 0m,
              Enums.CalculationType.DailyRate => StaticAmount.HasValue ? Math.Round(StaticAmount.Value * numberOfNights, 2) : 0m,
              Enums.CalculationType.PerUnit => StaticAmount.HasValue ? Math.Round(StaticAmount.Value * units, 2) : 0m,
                _ => 0m
            };
        }
    }
}

