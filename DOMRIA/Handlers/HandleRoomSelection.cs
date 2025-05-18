using System.Net.Http;
using DOMRIA.Models;
using Microsoft.AspNetCore.Mvc;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;

namespace DOMRIA.Handlers
{
    public class RoomSelectionHandler : BaseHandler
    {
        public RoomSelectionHandler(IHttpClientFactory httpClientFactory, ITelegramBotClient bot)
            : base(httpClientFactory, bot) { }

        public async Task<IActionResult> HandleRoomSelection(
            string messageText,
            long chatId,
            UserSearchState state,
            Func<long, UserSearchState, Task<IActionResult>> ShowRoomSelection,
            Func<long, UserSearchState, Task<IActionResult>> ShowDistrictSelection,
            Func<long, UserSearchState, Task<IActionResult>> ShowNextFlats,
            Func<long, UserSearchState, Task<IActionResult>> PromptFilterChange,
            Func<long, long, Task<IActionResult>> HandleStartCommand
        )
        {
            if (messageText.StartsWith("➡️"))
            {
                if (state.PreviousStep == "filter_select")
                {
                    state.CurrentIndex = 0;
                    state.CurrentPage = 0;
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
                if (state.RoomCountOptions.Count == 0)
                {
                    await _bot.SendMessage(
                        chatId,
                        "⚠️ Будь ласка, оберіть хоча б одну кімнату перед тим, як продовжити."
                    );
                    return Ok();
                }
                state.Step = "districts";
                state.Districts.Clear();
                if (!await TrySaveUserState(state, chatId))
                    return Ok();
                return await ShowDistrictSelection(chatId, state);
            }

            if (messageText.StartsWith("⬅️"))
            {
                state.Step = "filter_select";
                state.PreviousStep = null;
                if (!await TrySaveUserState(state, chatId))
                    return Ok();
                return await PromptFilterChange(chatId, state);
            }

            if (int.TryParse(messageText.Replace("✅", "").Trim(), out var selectedRoom))
            {
                if (selectedRoom < 1 || selectedRoom > 5)
                {
                    await _bot.SendMessage(chatId, "⚠️ Кількість кімнат має бути від 1 до 5.");
                    return Ok();
                }

                if (state.RoomCountOptions.Contains(selectedRoom))
                    state.RoomCountOptions.Remove(selectedRoom);
                else
                    state.RoomCountOptions.Add(selectedRoom);

                if (!await TrySaveUserState(state, chatId))
                    return Ok();

                // Завжди оновлюємо клавіатуру з галочками
                var buttons = Enumerable
                    .Range(1, 4)
                    .Select(n => new KeyboardButton(
                        (state.RoomCountOptions.Contains(n) ? "✅ " : "") + n
                    ))
                    .Chunk(2)
                    .Select(chunk => chunk.ToArray())
                    .ToList();

                buttons.Add(new[] { new KeyboardButton("➡️ Далі") });
                if (state.PreviousStep == "filter_select")
                    buttons.Add(new[] { new KeyboardButton("⬅️ Назад") });

                var markup = new ReplyKeyboardMarkup(buttons) { ResizeKeyboard = true };

                await _bot.SendMessage(
                    chatId,
                    "✅ Ви можете обрати ще або натиснути ➡️ Далі, щоб продовжити.",
                    replyMarkup: markup
                );
                return Ok();
            }

            // Некоректне введення
            await _bot.SendMessage(
                chatId,
                "⚠️ Будь ласка, оберіть кількість кімнат за допомогою кнопок нижче."
            );
            return Ok();
        }
    }
}
