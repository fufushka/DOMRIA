using System.Net.Http;
using System.Text.Json;
using Telegram.Bot;

namespace DOMRIA.Helpers
{
    public class UserStateHelper
    {
        private readonly HttpClient _http;
        protected readonly ITelegramBotClient _bot;

        public UserStateHelper(IHttpClientFactory httpClientFactory, ITelegramBotClient bot)
        {
            _http = httpClientFactory.CreateClient("BotClient");
            _bot = bot;
        }

        public async Task<UserSearchState> GetUserState(long userId)
        {
            var result = await _http.GetAsync($"/api/user/{userId}");
            return result.IsSuccessStatusCode
                ? await result.Content.ReadFromJsonAsync<UserSearchState>()
                : new UserSearchState { UserId = userId };
        }

        public async Task<bool> TrySaveUserState(UserSearchState state, long chatId)
        {
            try
            {
                var response = await _http.PostAsJsonAsync("/api/user", state);
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
    }
}
