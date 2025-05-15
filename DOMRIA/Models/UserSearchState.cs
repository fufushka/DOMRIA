using System.Text.Json.Serialization;
using DOMRIA.Models;
using MongoDB.Bson.Serialization.Attributes;

public class UserSearchState
{
    [BsonId] // 👈 робимо UserId головним ключем (_id)
    public long UserId { get; set; }
    public int CurrentPage { get; set; } = 1;

    public List<int> RoomCountOptions { get; set; } = new();
    public List<DistrictDto> Districts { get; set; } = new();
    public List<DistrictDto> AvailableDistricts { get; set; } = new();
    public int? MinPrice { get; set; }
    public int? MaxPrice { get; set; }

    public List<int> MatchingFlats { get; set; } = new();
    public int CurrentIndex { get; set; }
    public int TotalFlatCount { get; set; }
    public List<int> FavoriteFlatIds { get; set; } = new();
    public List<int> CompareFlatIds { get; set; } = new();
    public string? Step { get; set; }
    public string? PreviousStep { get; set; }
    public string? SortBy { get; set; } = "date"; // Можливі значення: "price", "area", "date"

    public bool NotFirstFloor { get; set; } = false;
    public bool NotLastFloor { get; set; } = false;
    public bool OnlyYeOselya { get; set; } = false;
    public List<int> NotifiedFlatIds { get; set; } = new();
}
