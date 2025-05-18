using Microsoft.AspNetCore.Mvc;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;

namespace DOMRIA.Handlers
{
    public class CommandStartHandler : BaseHandler
    {
        public CommandStartHandler(IHttpClientFactory httpClientFactory, ITelegramBotClient bot)
            : base(httpClientFactory, bot) { }

        public async Task<IActionResult> HandleStartCommand(long chatId, long userId)
        {
            var state = new UserSearchState { UserId = userId };
            if (!await TrySaveUserState(state, chatId))
                return Ok();

            var keyboard = new ReplyKeyboardMarkup(
                new[] { new[] { new KeyboardButton("🔍 Знайти квартиру") } }
            )
            {
                ResizeKeyboard = true,
            };

            await _bot.SendMessage(chatId, "Привіт! Обери дію:", replyMarkup: keyboard);
            return Ok();
        }
    }
}
