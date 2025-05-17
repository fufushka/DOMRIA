using System.Net.Http;
using System.Net.Http.Json;
using DOMRIA.Models;
using Microsoft.AspNetCore.Mvc;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;

namespace DOMRIA.Helpers
{
    public class SearchStepHelper
    {
        private readonly ITelegramBotClient _bot;
        private readonly HttpClient _http;

        public SearchStepHelper(ITelegramBotClient bot, IHttpClientFactory httpFactory)
        {
            _bot = bot;
            _http = httpFactory.CreateClient("BotClient");
        }

        public async Task<IActionResult> StartSearch(
            long chatId,
            UserSearchState state,
            Func<UserSearchState, long, Task<bool>> TrySaveUserState
        )
        {
            state.Step = "rooms";
            state.RoomCountOptions.Clear();
            if (!await TrySaveUserState(state, chatId))
                return new OkResult();
            return await ShowRoomSelection(chatId, state);
        }

        public async Task<IActionResult> ShowRoomSelection(long chatId, UserSearchState state)
        {
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
            {
                buttons.Add(new[] { new KeyboardButton("⬅️ Назад") });
            }
            var markup = new ReplyKeyboardMarkup(buttons) { ResizeKeyboard = true };
            await _bot.SendMessage(chatId, "Скільки кімнат ви шукаєте?", replyMarkup: markup);
            return new OkResult();
        }

        public async Task<IActionResult> ShowDistrictSelection(long chatId, UserSearchState state)
        {
            if (state.AvailableDistricts.Count == 0)
            {
                state.AvailableDistricts = await _http.GetFromJsonAsync<List<DistrictDto>>(
                    "/api/flat/districts"
                );
            }

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
            await _bot.SendMessage(chatId, "Оберіть район (можна декілька):", replyMarkup: markup);
            return new OkResult();
        }

        public async Task<IActionResult> ShowBudgetOptions(long chatId)
        {
            var priceRanges = new[]
            {
                "до 45000",
                "45000-55000",
                "55000-65000",
                "65000-80000",
                "80000-100000",
                "100000-200000",
                "200000+",
            };
            var buttons = priceRanges
                .Chunk(2)
                .Select(r => r.Select(p => new KeyboardButton(p)).ToArray())
                .ToList();
            buttons.Add(new[] { new KeyboardButton("⬅️ Назад") });

            var markup = new ReplyKeyboardMarkup(buttons) { ResizeKeyboard = true };
            await _bot.SendMessage(chatId, "Оберіть бажаний діапазон ціни:", replyMarkup: markup);
            return new OkResult();
        }

        public async Task<IActionResult> ShowSpecialFilterOptions(
            long chatId,
            UserSearchState state
        )
        {
            var buttons = new List<KeyboardButton[]>
            {
                new[] { new KeyboardButton((state.OnlyYeOselya ? "✅ " : "") + "🇺🇦 ЄОселя") },
                new[]
                {
                    new KeyboardButton((state.NotFirstFloor ? "✅ " : "") + "🧱 Не перший поверх"),
                },
                new[]
                {
                    new KeyboardButton((state.NotLastFloor ? "✅ " : "") + "🏢 Не останній поверх"),
                },
                new[] { new KeyboardButton("⬅️ Назад"), new KeyboardButton("➡️ Далі") },
            };

            var markup = new ReplyKeyboardMarkup(buttons) { ResizeKeyboard = true };
            await _bot.SendMessage(
                chatId,
                "Оберіть спеціальні побажання (можна декілька):",
                replyMarkup: markup
            );
            return new OkResult();
        }

        public async Task<IActionResult> PromptSortSelection(long chatId, UserSearchState state)
        {
            var keyboard = new ReplyKeyboardMarkup(
                new[]
                {
                    new[]
                    {
                        new KeyboardButton("💰 Спочатку дешеві"),
                        new KeyboardButton("💰 Спочатку дорогі"),
                    },
                    new[] { new KeyboardButton("🕒 Новіші") },
                }
            )
            {
                ResizeKeyboard = true,
            };

            await _bot.SendMessage(chatId, "Оберіть тип сортування:", replyMarkup: keyboard);
            return new OkResult();
        }

        public async Task<IActionResult> PromptFilterChange(long chatId, UserSearchState state)
        {
            var keyboard = new ReplyKeyboardMarkup(
                new[]
                {
                    new[] { new KeyboardButton("🛏 Кількість кімнат") },
                    new[] { new KeyboardButton("📍 Район") },
                    new[] { new KeyboardButton("💰 Бюджет") },
                    new[] { new KeyboardButton("🔄 Скинути всі фільтри") },
                }
            )
            {
                ResizeKeyboard = true,
            };

            await _bot.SendMessage(chatId, "Що саме хочете змінити?", replyMarkup: keyboard);
            return new OkResult();
        }

        public ReplyKeyboardMarkup GetMainMenuMarkup() =>
            new(
                new[]
                {
                    new[] { new KeyboardButton("➡️ Далі") },
                    new[] { new KeyboardButton("⚙️ Змінити фільтр пошуку") },
                    new[] { new KeyboardButton("🔃 Змінити сортування") },
                    new[] { new KeyboardButton("💌 Обрані квартири") },
                    new[] { new KeyboardButton("📝 Спеціальні побажання") },
                    new[] { new KeyboardButton("🆚 Порівняти обрані") },
                }
            )
            {
                ResizeKeyboard = true,
            };
    }
}
