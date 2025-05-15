using System;
using System.Text;
using System.Text.Json;
using DomRia.Config;
using DOMRIA.Models;
using DOMRIA.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

public class DomRiaService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;

    public DomRiaService(IOptions<BotSettings> config)
    {
        _httpClient = new HttpClient();
        _apiKey = config.Value.DomRiaApiKey;
    }

    public async Task<List<DistrictDto>> GetDistrictsAsync()
    {
        try
        {
            Console.WriteLine("Запит до DomRia API при пошуку районів: ");
            var url = $"https://developers.ria.com/dom/cities_districts/10?api_key={_apiKey}";
            var response = await _httpClient.GetStringAsync(url);

            var result = new List<DistrictDto>();
            var json = JsonDocument.Parse(response).RootElement;

            // Перший рівень — це масив масивів => беремо перший масив
            if (json.ValueKind == JsonValueKind.Array && json.GetArrayLength() > 0)
            {
                var innerArray = json[0]; // беремо внутрішній масив

                foreach (var item in innerArray.EnumerateArray())
                {
                    if (
                        item.TryGetProperty("isAdministrative", out var isAdmin)
                        && isAdmin.GetInt32() == 1
                    )
                    {
                        var name = item.GetProperty("name").GetString();
                        var id = item.GetProperty("area_id").GetInt32();

                        result.Add(new DistrictDto { Id = id, Name = name });
                    }
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [DomRiaService] GetDistrictsAsync: {ex.Message}");
            throw;
        }
    }

    public async Task<FlatSearchResponse> SearchFlatsByCriteriaAsync(
        List<int> roomCount,
        List<int> districtId,
        int? minPrice,
        int? maxPrice,
        string sortBy,
        int page = 0,
        int limit = 20,
        bool notFirstFloor = false,
        bool notLastFloor = false,
        bool onlyYeOselya = false
    )
    {
        try
        {
            var url = BuildSearchUrl(
                roomCount,
                districtId,
                minPrice,
                maxPrice,
                sortBy,
                page,
                limit,
                notFirstFloor,
                notLastFloor,
                onlyYeOselya
            );

            Console.WriteLine($"Запит до {url}");

            var response = await _httpClient.GetFromJsonAsync<FlatSearchResponse>(url);
            return response ?? new FlatSearchResponse { count = 0, items = new List<int>() };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [DomRiaService] SearchFlatsByCriteriaAsync: {ex.Message}");
            throw;
        }
    }

    private string BuildSearchUrl(
        List<int> roomCount,
        List<int> districtId,
        int? minPrice,
        int? maxPrice,
        string sortBy,
        int page,
        int limit,
        bool notFirstFloor = false,
        bool notLastFloor = false,
        bool onlyYeOselya = false,
        int roomCharacteristicId = 209,
        int priceCharacteristicId = 234,
        int yeCharacteristicId = 2001
    )
    {
        var builder = new StringBuilder();
        builder.Append($"https://developers.ria.com/dom/search?api_key={_apiKey}");
        builder.Append("&category=1&operation_type=1&city_id=10");

        AppendRoomCount(builder, roomCount, roomCharacteristicId);
        AppendDistricts(builder, districtId);
        AppendPriceRange(builder, minPrice, maxPrice, priceCharacteristicId);
        AppendSpecialFilters(
            builder,
            notFirstFloor,
            notLastFloor,
            onlyYeOselya,
            yeCharacteristicId
        );
        AppendSorting(builder, sortBy);

        builder.Append($"&page={page}");
        builder.Append($"&limit={limit}");

        return builder.ToString();
    }

    public async Task<FlatResult?> GetFlatByIdAsync(int id)
    {
        try
        {
            var url = $"https://developers.ria.com/dom/info/{id}?api_key={_apiKey}";
            var flat = await _httpClient.GetFromJsonAsync<FlatInfoResponse>(url);
            if (flat == null)
                return null;

            return new FlatResult
            {
                Title = flat.DescriptionUk?.Split('\n').FirstOrDefault() ?? "Опис відсутній",
                Price =
                    flat.Price != null && flat.CurrencyType != null
                        ? $"{flat.Price.Value.ToString("N0")} {flat.CurrencyType}"
                        : "Ціну не вказано",

                Url = string.IsNullOrEmpty(flat.BeautifulUrl)
                    ? "https://dom.ria.com/"
                    : $"https://dom.ria.com/uk/{flat.BeautifulUrl}",
                Area =
                    flat.TotalSquareMeters != null
                        ? flat.TotalSquareMeters.Value.ToString("0.##") + " м²"
                        : null,
                FloorInfo = flat.FloorInfo ?? "Поверх не вказано",
                Street = flat.StreetNameUk ?? "Вулиця не вказана",
                MetroStation = flat.MetroStationNameUk ?? "Станці метро не вказана",
                HousingComplex = flat.UserNewbuildNameUk ?? "ЖК не вказаний",
                AdminDistrict = flat.AdminDistrictNameUk,
                CityDistrict = flat.DistrictNameUk ?? "Район не вказаний",
                PublishedAt = flat.PublishingDateInfo ?? "Дата публікації не вказана",
                latitude = flat.Latitude,
                longitude = flat.Longitude,
                FlatId = flat.RealtyId,
                SupportsYeOselya =
                    flat.CharacteristicsValues?.TryGetValue("2001", out var el) == true
                    && el.GetInt32() == 2001,
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [DomRiaService] GetFlatByIdAsync: {ex.Message}");
            throw;
        }
    }

    private string MapSortOrder(string sortBy) =>
        sortBy switch
        {
            "price_up" => "p_a", // ціна за зростанням
            "price_down" => "p_d", // ціна за зростанням
            "date" => "created-at", // найновіші
            _ => "",
        };

    private void AppendRoomCount(
        StringBuilder builder,
        List<int> roomCount,
        int roomCharacteristicId
    )
    {
        if (roomCount.Any())
        {
            var min = Math.Min(roomCount.Min(), roomCount.Max());
            var max = Math.Max(roomCount.Min(), roomCount.Max());

            foreach (var val in roomCount.Distinct())
            {
                builder.Append($"&characteristic[{roomCharacteristicId}][]={val}");
            }
        }
    }

    private void AppendDistricts(StringBuilder builder, List<int> districtIds)
    {
        foreach (var id in districtIds)
        {
            builder.Append($"&district_id[]={id}");
        }
    }

    private void AppendPriceRange(
        StringBuilder builder,
        int? minPrice,
        int? maxPrice,
        int priceCharacteristicId
    )
    {
        if (minPrice != null)
            builder.Append($"&characteristic[{priceCharacteristicId}][from]={maxPrice}");
        if (maxPrice != null)
            builder.Append($"&characteristic[{priceCharacteristicId}][to]={minPrice}");
    }

    private void AppendSpecialFilters(
        StringBuilder builder,
        bool notFirstFloor,
        bool notLastFloor,
        bool onlyYeOselya,
        int yeCharacteristicId
    )
    {
        if (notFirstFloor)
            builder.Append("&notFirstFloor=1");
        if (notLastFloor)
            builder.Append("&notLastFloor=1");
        if (onlyYeOselya)
            builder.Append($"&characteristic[{yeCharacteristicId}]={yeCharacteristicId}");
    }

    private void AppendSorting(StringBuilder builder, string sortBy)
    {
        var order = MapSortOrder(sortBy);
        if (!string.IsNullOrEmpty(order))
            builder.Append($"&sort={order}");
    }
}
