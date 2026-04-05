namespace ApplicationCore.Dtos
{
    /// <summary>
    /// DTO for automatic fee rules used in the Admin Fee Upsert page.
    /// This is the structure stored in Fee.TriggerRuleJson.
    /// </summary>
    public class AutomaticRuleDto
    {
        public string Field { get; set; } = "";
        public string? FieldType { get; set; }
        public string Operator { get; set; } = "";
        public object? Value { get; set; }
    }
}