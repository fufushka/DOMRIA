using System.Text.Json;
using System.Text.Json.Serialization;

namespace DOMRIA.Models
{
    public class FlatInfoResponse
    {
        [JsonPropertyName("description_uk")]
        public string? DescriptionUk { get; set; }

        [JsonPropertyName("price")]
        public long? Price { get; set; }

        [JsonPropertyName("currency_type")]
        public string? CurrencyType { get; set; }

        [JsonPropertyName("beautiful_url")]
        public string? BeautifulUrl { get; set; }

        [JsonPropertyName("total_square_meters")]
        public decimal? TotalSquareMeters { get; set; }

        [JsonPropertyName("floor_info")]
        public string? FloorInfo { get; set; }

        [JsonPropertyName("street_name_uk")]
        public string? StreetNameUk { get; set; }

        [JsonPropertyName("metro_station_name_uk")]
        public string? MetroStationNameUk { get; set; }

        [JsonPropertyName("user_newbuild_name_uk")]
        public string? UserNewbuildNameUk { get; set; }

        [JsonPropertyName("admin_district_name_uk")]
        public string? AdminDistrictNameUk { get; set; }

        [JsonPropertyName("district_name_uk")]
        public string? DistrictNameUk { get; set; }

        [JsonPropertyName("publishing_date_info")]
        public string? PublishingDateInfo { get; set; }

        [JsonPropertyName("latitude")]
        public double? Latitude { get; set; }

        [JsonPropertyName("longitude")]
        public double? Longitude { get; set; }

        [JsonPropertyName("realty_id")]
        public int RealtyId { get; set; }

        [JsonPropertyName("characteristics_values")]
        public Dictionary<string, JsonElement>? CharacteristicsValues { get; set; }
    }
}
