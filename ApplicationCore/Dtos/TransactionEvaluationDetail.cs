using System.Text;

namespace ApplicationCore.Dtos
{
    /// <summary>
    /// Represents the details of a transaction evaluation, including fee calculations and breakdowns.
    /// </summary>
    public class TransactionEvaluationDetail
    {
        public int FeeId { get; set; }
        public string FeeName { get; set; } = "";
        public string CalculationType { get; set; } = "StaticAmount";

        public int Units { get; set; }
        public decimal Amount { get; set; }
        public decimal PerUnitAmount { get; set; }

        public bool PerUnit { get; set; }
        public bool IsDailyRate { get; set; }
        public decimal DailyRate { get; set; }
        public int NumberOfNights { get; set; }

        public decimal BaseAmount { get; set; }

        public Dictionary<string, object> TriggerInputs { get; set; } = new();

        public decimal ComputedPerUnitAmount =>
            Units > 0
                ? Amount / (IsDailyRate ? Units * NumberOfNights : Units)
                : 0;

        public string TriggerRuleSnapshotJson { get; set; } = "";
        public string Breakdown { get; set; } = "";

        /// <summary>
        /// Provides a detailed breakdown of the fee calculation based on the current configuration.
        /// </summary>
        /// <returns>A string representing the breakdown of the fee calculation. If the amount is less than or equal to zero, the
        /// method returns "No fee applied."</returns>
        public string GetBreakdown()
        {
            if (Amount <= 0) return "No fee applied.";

            var sb = new StringBuilder();
            var totalFormatted = Amount.ToString("C");

            if (CalculationType == "StaticAmount")
            {
                if (PerUnit && IsDailyRate)
                {
                    sb.Append($"{NumberOfNights} night(s) × {Units} unit(s) × {ComputedPerUnitAmount:C} = {totalFormatted}");
                }
                else if (PerUnit)
                {
                    sb.Append($"{Units} unit(s) × {ComputedPerUnitAmount:C} = {totalFormatted}");
                }
                else if (IsDailyRate)
                {
                    sb.Append($"{NumberOfNights} night(s) × {ComputedPerUnitAmount:C} = {totalFormatted}");
                }
                else
                {
                    sb.Append($"Flat fee: {totalFormatted}");
                }
            }
            else if (CalculationType == "PerUnit")
            {
                sb.Append($"{Units} unit(s) × {ComputedPerUnitAmount:C}");
                if (IsDailyRate)
                {
                    sb.Append($" × {NumberOfNights} night(s)");
                }
                sb.Append($" = {totalFormatted}");
            }
            else if (CalculationType == "PercentOfBalance")
            {
                var percentage = BaseAmount != 0 ? Amount / BaseAmount : 0;
                sb.Append($"{BaseAmount:C} × {percentage:P} = {totalFormatted}");
            }
            else if (CalculationType == "DailyRate")
            {
                sb.Append($"{NumberOfNights} night(s)");
                if (PerUnit) sb.Append($" × {Units} unit(s)");
                sb.Append($" × {DailyRate:C} = {totalFormatted}");
            }
            else if (CalculationType == "CustomFlatFee")
            {
                sb.Append($"Flat fee applied: {totalFormatted}");
            }
            else
            {
                sb.Append($"Calculated: {totalFormatted}");
            }

            return sb.ToString();
        }

        public override string ToString()
        {
            return $"{FeeName}: {Amount:C} ({CalculationType}) - {Units} unit(s)" +
                   (IsDailyRate ? $" x {NumberOfNights} night(s)" : "") +
                   $"\nInputs: {string.Join(", ", TriggerInputs.Select(kvp => $"{kvp.Key}: {kvp.Value}"))}";
        }
    }
}
