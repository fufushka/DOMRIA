using System.Net.Http;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;

namespace DOMRIA.Handlers
{
    public abstract class BaseHandler
    {
        protected readonly HttpClient _httpClient;
        protected readonly ITelegramBotClient _bot;

        protected BaseHandler(IHttpClientFactory httpClientFactory, ITelegramBotClient bot)
        {
            _httpClient = httpClientFactory.CreateClient("BotClient");
            _bot = bot;
        }

        protected async Task<bool> TrySaveUserState(UserSearchState state, long chatId)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("/api/user", state);
                var json = JsonSerializer.Serialize(state);
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"❌ POST /api/user failed: {response.StatusCode}");
                    await _bot.SendMessage(chatId, "⚠️ Не вдалося зберегти зміни через API.");
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ TrySaveUserState API error: {ex.Message}");
                await _bot.SendMessage(
                    chatId,
                    "⚠️ Виникла помилка при збереженні. Спробуйте пізніше."
                );
                return false;
            }
        }

        private ReplyKeyboardMarkup GetMainMenuMarkup() =>
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

        protected async Task SendNoResults(long chatId)
        {
            await _bot.SendMessage(chatId, "⚠️ За вашими критеріями нічого не знайдено.");
        }

        protected IActionResult Ok() => new OkResult();

        protected IActionResult NotFound() => new NotFoundResult();

        protected IActionResult BadRequest() => new BadRequestResult();
    }
}
