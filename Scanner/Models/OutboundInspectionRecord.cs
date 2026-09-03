namespace Scanner.Models
{
    public sealed class OutboundInspectionRecord
    {
        public int Number { get; set; }
        public string Sn { get; set; }
        public string Sku { get; set; }
        public string Type { get; set; }
        public string ProcessingMethod { get; set; }
        public string ProcessingTime { get; set; }
        public bool IsMatched { get; set; }
    }
}
