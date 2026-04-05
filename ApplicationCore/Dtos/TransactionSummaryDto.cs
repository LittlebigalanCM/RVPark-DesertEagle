using ApplicationCore.Enums;

namespace ApplicationCore.Dtos
{
    /// <summary>
    /// Represents a summary of a transaction, including details such as label, amount, fee, and calculation type.
    /// </summary>
    public class TransactionSummaryDto
    {
        public string Label { get; set; } = "";
        public decimal Amount { get; set; }
        public int FeeId { get; set; }
        public int Units { get; set; }
        public int NumberOfNights { get; set; }
        public decimal PerUnitAmount { get; set; }
        public decimal Percentage { get; set; }
        public CalculationType CalculationType { get; set; }
        public bool IsPerNight { get; set; }
        public string Status { get; set; } = "Unpaid";
        public DateTime TransactionDateTime { get; set; } = DateTime.Now;

        /// <summary>
        /// Generates a descriptive text breakdown of the calculation based on the current calculation type.
        /// </summary>
        /// <returns>A string representing the breakdown of the calculation. The format varies depending on the calculation type.</returns>
        public string GetBreakdownText()
        {
            return CalculationType switch
            {
                CalculationType.StaticAmount =>
                    "Flat fee",

                CalculationType.PercentOfTotal =>
                    $"{Percentage}% of base",

                CalculationType.PerUnit when IsPerNight =>
                    $"{Units} extra × {PerUnitAmount:C} × {NumberOfNights} nights",

                CalculationType.PerUnit =>
                    $"{Units} × {PerUnitAmount:C}",

                CalculationType.DailyRate =>
                    $"{NumberOfNights} nights × {PerUnitAmount:C}",

                _ => ""
            };
        }
    }
}
