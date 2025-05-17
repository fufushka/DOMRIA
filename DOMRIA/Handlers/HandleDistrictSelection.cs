using DOMRIA.Models;
using Microsoft.AspNetCore.Mvc;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;

namespace DOMRIA.Handlers
{
    public class DistrictSelectionHandler : BaseHandler
    {
        public DistrictSelectionHandler(
            IHttpClientFactory httpClientFactory,
            ITelegramBotClient bot
        )
            : base(httpClientFactory, bot) { }

        public async Task<IActionResult> HandleDistrictSelection(
            string messageText,
            long chatId,
            UserSearchState state,
            Func<long, Task<IActionResult>> ShowBudgetOptions,
            Func<long, UserSearchState, Task<IActionResult>> ShowNextFlats,
            Func<long, UserSearchState, Task<IActionResult>> ShowDistrictSelection,
            Func<long, UserSearchState, Task<IActionResult>> PromptFilterChange,
            Func<long, UserSearchState, Task<IActionResult>> ShowRoomSelection
        )
        {
            state.Step = "districts";
            await TrySaveUserState(state, chatId);
            if (messageText.StartsWith("➡️"))
            {
                if (state.PreviousStep == "filter_select")
                {
                    state.Step = "done";
                    state.PreviousStep = null;
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
                if (state.Districts.Count == 0)
                {
                    await _bot.SendMessage(
                        chatId,
                        "⚠️ Будь ласка, оберіть хоча б один район перед тим, як продовжити."
                    );
                    return Ok();
                }
                state.Step = "budget";

                if (!await TrySaveUserState(state, chatId))
                    return Ok();
                return await ShowBudgetOptions(chatId);
            }

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

                state.Step = "rooms";
                if (!await TrySaveUserState(state, chatId))
                    return Ok();
                return await ShowRoomSelection(chatId, state);
            }

            // Завантаження районів, якщо потрібно
            if (state.AvailableDistricts.Count == 0)
            {
                state.AvailableDistricts = await _httpClient.GetFromJsonAsync<List<DistrictDto>>(
                    "/api/flat/districts"
                );
            }

            var selectedName = messageText.Replace("✅", "").Trim();
            var dto = state.AvailableDistricts.FirstOrDefault(d => d.Name == selectedName);

            if (dto != null)
            {
                if (state.Districts.Any(d => d.Id == dto.Id))
                    state.Districts.RemoveAll(d => d.Id == dto.Id);
                else
                    state.Districts.Add(dto);

                if (!await TrySaveUserState(state, chatId))
                    return Ok();

                var buttons = state
                    .AvailableDistricts.Select(d => new KeyboardButton(
                        (state.Districts.Any(sd => sd.Id == d.Id) ? "✅ " : "") + d.Name
                    ))
                    .Chunk(2)
                    .Select(chunk => chunk.ToArray())
                    .ToList();

                buttons.Add(new[] { new KeyboardButton("➡️ Далі") });
                buttons.Add(new[] { new KeyboardButton("⬅️ Назад") });

                var markup = new ReplyKeyboardMarkup(buttons) { ResizeKeyboard = true };

                await _bot.SendMessage(
                    chatId,
                    "✅ Район оновлено. Ви можете обрати ще або натиснути ➡️ Далі, щоб продовжити.",
                    replyMarkup: markup
                );
                return Ok(); // ← ОБОВʼЯЗКОВО!
            }

            if (!await TrySaveUserState(state, chatId))
                return Ok();
            await _bot.SendMessage(
                chatId,
                "⚠️ Будь ласка, оберіть райони за допомогою кнопок нижче."
            );
            return Ok();
        }
    }
}
