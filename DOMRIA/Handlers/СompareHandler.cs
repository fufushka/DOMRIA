using DOMRIA.Models;
using Microsoft.AspNetCore.Mvc;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace DOMRIA.Handlers;

public class CompareHandler : BaseHandler
{
    public CompareHandler(IHttpClientFactory httpClientFactory, ITelegramBotClient bot)
        : base(httpClientFactory, bot) { }

    public async Task<IActionResult> HandleCompareCallback(
        CallbackQuery callback,
        UserSearchState state
    )
    {
        var chatId = callback.Message.Chat.Id;
        var userId = callback.From.Id;
        var data = callback.Data;
        if (data.StartsWith("compare_"))
        {
            var command = data.Replace("compare_", "");

            if (int.TryParse(command, out int flatId))
            {
                if (state.CompareFlatIds.Contains(flatId))
                {
                    await _bot.AnswerCallbackQuery(
                        callback.Id,
                        "Ця квартира вже додана до порівняння"
                    );
                }
                else if (state.CompareFlatIds.Count >= 2)
                {
                    await _bot.AnswerCallbackQuery(
                        callback.Id,
                        "❗ Можна порівняти лише 2 квартири"
                    );
                }
                else
                {
                    state.CompareFlatIds ??= new List<int>();
                    state.CompareFlatIds.Add(flatId);
                    if (!await TrySaveUserState(state, chatId))
                        return Ok();
                    await _bot.AnswerCallbackQuery(callback.Id, "✅ Додано до порівняння");
                }
            }
        }
        if (data == "compare_show")
        {
            return await ShowComparison(chatId, state);
        }
        if (data == "compare_reset")
        {
            state.CompareFlatIds ??= new List<int>();
            state.CompareFlatIds.Clear();
            await TrySaveUserState(state, chatId);
            await _bot.DeleteMessage(chatId, callback.Message.MessageId);

            await _bot.AnswerCallbackQuery(callback.Id, "✅ Порівняння очищено");
        }

        return Ok();
    }

    public async Task<IActionResult> ShowComparison(long chatId, UserSearchState state)
    {
        if (state.CompareFlatIds.Count < 1)
        {
            await _bot.SendMessage(
                chatId,
                "У вас не вибрано жодної квартири для порівняння (потрібно 2) 🧐"
            );
            return Ok();
        }
        if (state.CompareFlatIds.Count < 2)
        {
            await _bot.SendMessage(chatId, "Додайте ще одну квартиру для порівняння  🧐");
            return Ok();
        }

        var flat1 = await _httpClient.GetFromJsonAsync<FlatResult>(
            $"/api/flat/{state.CompareFlatIds[0]}"
        );
        var flat2 = await _httpClient.GetFromJsonAsync<FlatResult>(
            $"/api/flat/{state.CompareFlatIds[1]}"
        );

        if (flat1 == null || flat2 == null)
        {
            await _bot.SendMessage(chatId, "❌ Не вдалося завантажити інформацію про квартири.");
            return Ok();
        }

        string msg = $"""
🔎 Порівняння:
 🏠 <a href="{flat1.Url}">#{flat1.FlatId}</a>  |  🏠 <a href="{flat2.Url}">#{flat2.FlatId}</a>
💰 {flat1.Price}  |  💰 {flat2.Price}
📐 {flat1.Area}   |  📐 {flat2.Area}
🏢 {flat1.FloorInfo} | 🏢 {flat2.FloorInfo}
📍 {flat1.Street} | 📍 {flat2.Street}
🚇 {flat1.MetroStation} | 🚇 {flat2.MetroStation}
🌍 {flat1.AdminDistrict} | 🌍 {flat2.AdminDistrict}
🕒 {flat1.PublishedAt} | 🕒 {flat2.PublishedAt}
🇺🇦 ЄОселя: {(flat1.SupportsYeOselya ? "✅" : "❌")} | {(flat2.SupportsYeOselya ? "✅" : "❌")}
""";

        var buttons = new InlineKeyboardMarkup(
            new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData(
                        "🗑 Очистити порівняння",
                        "compare_reset"
                    ),
                },
            }
        );

        await _bot.SendMessage(chatId, msg, parseMode: ParseMode.Html, replyMarkup: buttons);
        return Ok();
    }
}
