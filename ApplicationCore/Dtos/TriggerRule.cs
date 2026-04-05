namespace ApplicationCore.Dtos
{
    /// <summary>
    /// Represents a rule used to trigger actions based on specified conditions.
    /// </summary>
    public class TriggerRule
    {
        public string Field { get; set; } = "";
        public string? FieldType { get; set; }
        public string Operator { get; set; } = "";
        public object? Value { get; set; }

        public int BaseIncluded { get; set; }
        public bool PerUnit { get; set; }
    }
}
