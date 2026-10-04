namespace Scanner.Models
{
    public sealed class ShelvedPalletBatchRequest
    {
        public string PalletNumber { get; set; }
        public IList<ShelvedPalletItem> Items { get; set; }
    }

    public sealed class ShelvedPalletItem
    {
        public int Number { get; set; }
        public string Sn { get; set; }
        public string Sku { get; set; }
        public string Type { get; set; }
        public string ProcessingMethod { get; set; }
        public string ProcessingTime { get; set; }
    }

    public sealed class ShelvedPalletUploadResult
    {
        public string PalletNumber { get; set; }
        public DateTime ShelvedAt { get; set; }
        public int InsertedCount { get; set; }
        public IList<string> Uuids { get; set; }
    }
}
