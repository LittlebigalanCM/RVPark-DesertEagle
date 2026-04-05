using System.ComponentModel.DataAnnotations;

namespace ApplicationCore.Enums
{
    /// <summary>
    /// Specifies the type of trigger that initiates an action or event.
    /// </summary>
    public enum TriggerType
    {
        [Display(Name = "Manual (Added by Staff)")]
        Manual = 0,
        [Display(Name = "Automatic (Added by System Based on Rules)")]
        Automatic = 1
    }

    /// <summary>
    /// Specifies the type of calculation to be applied in financial or quantity-based operations.
    /// </summary>
    public enum CalculationType
    {
        [Display(Name = "Flat Amount")]
        StaticAmount = 0,
        [Display(Name = "Percentage of Total")]
        PercentOfTotal = 1,
        [Display(Name = "Daily Rate")]
        DailyRate = 2,
        [Display(Name = "Amount Per Unit")]
        PerUnit = 3,
    }
}
