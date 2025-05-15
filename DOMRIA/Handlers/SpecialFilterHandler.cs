using DOMRIA.Models;
using Microsoft.AspNetCore.Mvc;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;

namespace DOMRIA.Handlers
{
    public class SpecialFilterHandler : BaseHandler
    {
        public SpecialFilterHandler(IHttpClientFactory httpClientFactory, ITelegramBotClient bot)
            : base(httpClientFactory, bot) { }

        public async Task<IActionResult> HandleSpecialFilters(
            string messageText,
            long chatId,
            UserSearchState state,
            Func<long, UserSearchState, Task<IActionResult>> ShowNextFlats,
            Func<long, UserSearchState, Task<IActionResult>> ShowSpecialFilterOptions,
            Func<ReplyKeyboardMarkup> GetMainMenuMarkup
        )
        {
            if (messageText.Contains("ЄОселя"))
                state.OnlyYeOselya = !state.OnlyYeOselya;
            else if (messageText.Contains("Не перший поверх"))
                state.NotFirstFloor = !state.NotFirstFloor;
            else if (messageText.Contains("Не останній поверх"))
                state.NotLastFloor = !state.NotLastFloor;
            else if (messageText == "⬅️ Назад")
            {
                state.Step = "done";
                state.CurrentIndex = 0;
                state.CurrentPage = 0;

                var response = await _httpClient.PostAsJsonAsync("/api/flat/search", state);
                var result = await response.Content.ReadFromJsonAsync<FlatSearchResponse>();
                state.MatchingFlats = result?.items ?? new List<int>();
                state.TotalFlatCount = result?.count ?? 0;

                if (!await TrySaveUserState(state, chatId))
                    return Ok();

                await _bot.SendMessage(
                    chatId,
                    $"Спеціальні побажання оновлено, йдемо далі?",
                    replyMarkup: GetMainMenuMarkup()
                );

                return Ok();
            }
            else if (messageText == "➡️ Далі")
            {
                state.Step = "done";
                state.CurrentIndex = 0;
                state.CurrentPage = 0;

                var response = await _httpClient.PostAsJsonAsync("/api/flat/search", state);
                var result = await response.Content.ReadFromJsonAsync<FlatSearchResponse>();
                state.MatchingFlats = result?.items ?? new List<int>();
                state.TotalFlatCount = result?.count ?? 0;
                if (!await TrySaveUserState(state, chatId))
                    return Ok();
                return await ShowNextFlats(chatId, state);
            }
            else
            {
                await _bot.SendMessage(
                    chatId,
                    "⚠️ Будь ласка, оберіть спец.побажання за допомогою кнопок нижче."
                );
                return Ok();
            }

            if (!await TrySaveUserState(state, chatId))
                return Ok();

            return await ShowSpecialFilterOptions(chatId, state);
        }
    }
}
