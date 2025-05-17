using System.Net.Http;
using System.Text;
using DOMRIA.Models;
using Microsoft.AspNetCore.DataProtection.KeyManagement;

namespace DOMRIA.Helpers;

public static class SearchUrlBuilder
{
    public static string BuildSearchUrl(
        string apiKey,
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
        builder.Append($"https://developers.ria.com/dom/search?api_key={apiKey}");
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

    public static string MapSortOrder(string sortBy) =>
        sortBy switch
        {
            "price_up" => "p_a", // ціна за зростанням
            "price_down" => "p_d", // ціна за зростанням
            "date" => "created-at", // найновіші
            _ => "",
        };

    public static void AppendRoomCount(
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

    public static void AppendDistricts(StringBuilder builder, List<int> districtIds)
    {
        foreach (var id in districtIds)
        {
            builder.Append($"&district_id[]={id}");
        }
    }

    public static void AppendPriceRange(
        StringBuilder builder,
        int? minPrice,
        int? maxPrice,
        int priceCharacteristicId
    )
    {
        if (minPrice.HasValue && maxPrice.HasValue && minPrice > maxPrice)
        {
            // ⚠️ Переставляємо значення, якщо помилково переплутані
            var temp = minPrice;
            minPrice = maxPrice;
            maxPrice = temp;
        }

        if (minPrice.HasValue)
            builder.Append($"&characteristic[{priceCharacteristicId}][from]={minPrice.Value}");

        if (maxPrice.HasValue)
            builder.Append($"&characteristic[{priceCharacteristicId}][to]={maxPrice.Value}");
    }

    public static void AppendSpecialFilters(
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

    public static void AppendSorting(StringBuilder builder, string sortBy)
    {
        var order = MapSortOrder(sortBy);
        if (!string.IsNullOrEmpty(order))
            builder.Append($"&sort={order}");
    }
}
