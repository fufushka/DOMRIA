namespace DOMRIA.Models
{
    public class FlatResult
    {
        public int FlatId { get; set; }
        public string Title { get; set; }
        public string Price { get; set; }
        public string Url { get; set; }
        public string Area { get; set; }
        public string FloorInfo { get; set; }
        public string Street { get; set; }
        public string MetroStation { get; set; }
        public string HousingComplex { get; set; }
        public string AdminDistrict { get; set; }
        public string CityDistrict { get; set; }
        public string PublishedAt { get; set; }
        public double? latitude { get; set; }
        public double? longitude { get; set; }
        public bool SupportsYeOselya { get; set; }
    }
}
