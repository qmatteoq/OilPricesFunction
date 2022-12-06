namespace OilPriceFunction.Models
{
    public record OilPrices
    {
        public OilPrice? CurrentKec { get; set; }

        public OilPrice? CurrentButane { get; set; }

        public OilPrice? PreviousButane { get; set; }

        public OilPrice? CurrentPropane { get; set; }

        public OilPrice? PreviousPropane { get; set; }

        public string? Error { get; set; }

    }

    public record OilPrice
    {
        public string? Price { get; set; }

        public string? Date { get; set; }
    }

}
