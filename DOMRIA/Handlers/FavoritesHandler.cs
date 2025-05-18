namespace DOMRIA.Handlers;

using System.Net.Http;
using System.Net.Http.Json;
using DOMRIA.Helpers;
using DOMRIA.Models;
using Microsoft.AspNetCore.Mvc;
using Telegram.Bot;

public class FavoritesHandler : BaseHandler
{
    private readonly UserStateHelper _userStateHelper;

    public FavoritesHandler(
        IHttpClientFactory httpClientFactory,
        ITelegramBotClient bot,
        UserStateHelper userStateHelper
    )
        : base(httpClientFactory, bot)
    {
        _userStateHelper = userStateHelper;
    }

    public async Task<IActionResult> ShowFavorites(long chatId, long userId)
    {
        var state = await _userStateHelper.GetUserState(userId);
        if (state.FavoriteFlatIds == null || !state.FavoriteFlatIds.Any())
        {
            await _bot.SendMessage(chatId, "У вас ще немає улюблених квартир.");
            return Ok();
        }

        foreach (var id in state.FavoriteFlatIds)
        {
            var flat = await _httpClient.GetFromJsonAsync<FlatResult>($"/api/flat/{id}");
            if (flat != null)
            {
                await _bot.SendFlatMessage(chatId, flat, state);
            }
        }
        return Ok();
    }
}
