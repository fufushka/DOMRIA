using System;
using System.Text;
using System.Text.Json;
using DomRia.Config;
using DOMRIA.Helpers;
using DOMRIA.Interfaces;
using DOMRIA.Models;
using DOMRIA.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

public class DomRiaService : IDomRiaService
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

    public async Task<FlatSearchResponse> SearchFlatsByCriteriaAsync(UserSearchState state)
    {
        try
        {
            var url = SearchUrlBuilder.BuildSearchUrl(
                apiKey: _apiKey,
                roomCount: state.RoomCountOptions,
                districtId: state.Districts.Select(d => d.Id).ToList(),
                minPrice: state.MinPrice,
                maxPrice: state.MaxPrice,
                sortBy: state.SortBy,
                page: state.CurrentPage,
                limit: 5,
                notFirstFloor: state.NotFirstFloor,
                notLastFloor: state.NotLastFloor,
                onlyYeOselya: state.OnlyYeOselya
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
}
