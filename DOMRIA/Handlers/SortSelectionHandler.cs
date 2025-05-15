using DOMRIA.Models;
using Microsoft.AspNetCore.Mvc;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;

namespace DOMRIA.Handlers
{
    public class SortSelectionHandler : BaseHandler
    {
        public SortSelectionHandler(IHttpClientFactory httpClientFactory, ITelegramBotClient bot)
            : base(httpClientFactory, bot) { }

        public async Task<IActionResult> HandleSortSelection(
            string messageText,
            long chatId,
            UserSearchState state,
            Func<long, UserSearchState, Task<IActionResult>> ShowNextFlats,
            Func<long, long, Task<IActionResult>> HandleStartCommand
        )
        {
            if (messageText == "💰 Спочатку дешеві")
                state.SortBy = "price_up";
            else if (messageText == "💰 Спочатку дорогі")
                state.SortBy = "price_down";
            else if (messageText == "🕒 Новіші")
                state.SortBy = "date";
            else
            {
                await _bot.SendMessage(
                    chatId,
                    "⚠️ Будь ласка, оберіть тип сортування за допомогою кнопок нижче."
                );
                return Ok();
            }

            state.CurrentIndex = 0;
            state.CurrentPage = 0;

            var response = await _httpClient.PostAsJsonAsync("/api/flat/search", state);
            var result = await response.Content.ReadFromJsonAsync<FlatSearchResponse>();
            state.MatchingFlats = result?.items ?? new List<int>();
            state.TotalFlatCount = result?.count ?? 0;

            if (!state.MatchingFlats.Any())
            {
                state.Step = null;
                await TrySaveUserState(state, chatId);

                await _bot.SendMessage(
                    chatId,
                    "⚠️ За вашими критеріями нічого не знайдено. Ви можете почати новий пошук."
                );
                return await HandleStartCommand(chatId, state.UserId);

                return Ok();
            }

            state.Step = "done";
            if (!await TrySaveUserState(state, chatId))
                return Ok();

            return await ShowNextFlats(chatId, state);
        }
    }
}
