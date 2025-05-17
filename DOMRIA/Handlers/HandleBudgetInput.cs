using DOMRIA.Models;
using Microsoft.AspNetCore.Mvc;
using Telegram.Bot;

namespace DOMRIA.Handlers
{
    public class BudgetInputHandler : BaseHandler
    {
        public BudgetInputHandler(IHttpClientFactory httpClientFactory, ITelegramBotClient bot)
            : base(httpClientFactory, bot) { }

        public async Task<IActionResult> HandleBudgetInput(
            string messageText,
            long chatId,
            UserSearchState state,
            Func<long, UserSearchState, Task<IActionResult>> ShowNextFlats,
            Func<long, UserSearchState, Task<IActionResult>> ShowDistrictSelection,
            Func<long, UserSearchState, Task<IActionResult>> PromptFilterChange,
            Func<long, UserSearchState, Task<IActionResult>> PromptSortSelection,
            Func<long, long, Task<IActionResult>> HandleStartCommand
        )
        {
            if (messageText.StartsWith("⬅️"))
            {
                if (state.PreviousStep == "filter_select")
                {
                    state.Step = "filter_select";
                    state.PreviousStep = null;
                    if (!await TrySaveUserState(state, chatId))
                        return Ok();
                    return await PromptFilterChange(chatId, state);
                }

                state.Step = "districts";
                if (!await TrySaveUserState(state, chatId))
                    return Ok();
                return await ShowDistrictSelection(chatId, state);
            }

            // Очищаємо текст
            var cleaned = messageText.Replace(" ", "").Replace("₴", "").Replace("грн", "");

            bool parsed = false;

            if (cleaned.Contains("-"))
            {
                var parts = cleaned.Split('-');
                if (int.TryParse(parts[0], out var min) && int.TryParse(parts[1], out var max))
                {
                    state.MinPrice = Math.Min(min, max);
                    state.MaxPrice = Math.Max(min, max);
                    parsed = true;
                }
            }
            else if (
                cleaned.StartsWith("до") && int.TryParse(cleaned.Replace("до", ""), out var maxVal)
            )
            {
                state.MinPrice = 0;
                state.MaxPrice = maxVal;
                parsed = true;
            }
            else if (
                cleaned.EndsWith("+") && int.TryParse(cleaned.Replace("+", ""), out var minVal)
            )
            {
                state.MinPrice = minVal;
                state.MaxPrice = null;
                parsed = true;
            }

            if (!parsed)
            {
                await _bot.SendMessage(
                    chatId,
                    "⚠️ Будь ласка, оберіть діапазон цін за допомогою кнопок нижче."
                );
                return Ok();
            }

            // Якщо редагуємо фільтри — одразу робимо пошук
            if (state.PreviousStep == "filter_select")
            {
                state.CurrentIndex = 0;
                state.CurrentPage = 1;

                var resp = await _httpClient.PostAsJsonAsync("/api/flat/search", state);
                var result = await resp.Content.ReadFromJsonAsync<FlatSearchResponse>();
                state.MatchingFlats = result?.items ?? new List<int>();
                state.TotalFlatCount = result?.count ?? 0;
                if (!state.MatchingFlats.Any())
                {
                    state.Step = null;
                    await TrySaveUserState(state, chatId);

                    await SendNoResults(chatId);
                    return await HandleStartCommand(chatId, state.UserId);
                }
                state.Step = "done";
                state.PreviousStep = null;

                if (!await TrySaveUserState(state, chatId))
                    return Ok();

                return await ShowNextFlats(chatId, state);
            }

            // Стандартний шлях: далі вибір сортування
            state.Step = "sort_select";
            if (!await TrySaveUserState(state, chatId))
                return Ok();
            return await PromptSortSelection(chatId, state);
        }
    }
}
