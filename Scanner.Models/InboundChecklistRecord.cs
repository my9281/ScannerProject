namespace Scanner.Models
{
    public sealed class InboundChecklistRecord
    {
        public string Sn { get; set; }
        public string Sku { get; set; }
        public string RmaNumber { get; set; }
        public string Type { get; set; }
        public string DetectionStatus { get; set; }
        public string ProcessingTime { get; set; }
    }
}
