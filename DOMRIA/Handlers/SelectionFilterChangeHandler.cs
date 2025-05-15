using Microsoft.AspNetCore.Mvc;
using Telegram.Bot;

namespace DOMRIA.Handlers
{
    public class SelectionFilterChangeHandler : BaseHandler
    {
        public SelectionFilterChangeHandler(
            IHttpClientFactory httpClientFactory,
            ITelegramBotClient bot
        )
            : base(httpClientFactory, bot) { }

        public async Task<IActionResult> HandleFilterChangeSelection(
            string messageText,
            long chatId,
            UserSearchState state,
            Func<long, UserSearchState, Task<IActionResult>> ShowRoomSelection,
            Func<long, UserSearchState, Task<IActionResult>> ShowDistrictSelection,
            Func<long, Task<IActionResult>> ShowBudgetOptions
        )
        {
            if (messageText == "🛏 Кількість кімнат")
            {
                state.PreviousStep = state.Step;
                state.Step = "rooms";
                if (!await TrySaveUserState(state, chatId))
                    return Ok();
                return await ShowRoomSelection(chatId, state);
            }

            if (messageText == "📍 Район")
            {
                state.PreviousStep = state.Step;
                state.Step = "districts";
                if (!await TrySaveUserState(state, chatId))
                    return Ok();
                return await ShowDistrictSelection(chatId, state);
            }
            if (messageText == "💰 Бюджет")
            {
                state.PreviousStep = state.Step;
                state.Step = "budget";
                if (!await TrySaveUserState(state, chatId))
                    return Ok();
                return await ShowBudgetOptions(chatId);
            }
            if (messageText == "🔄 Скинути всі фільтри")
            {
                state.RoomCountOptions.Clear();
                state.Districts.Clear();
                state.AvailableDistricts.Clear();
                state.MinPrice = null;
                state.MaxPrice = null;
                state.MatchingFlats.Clear();
                state.CurrentIndex = 0;
                state.Step = "rooms";
                if (!await TrySaveUserState(state, chatId))
                    return Ok();
                await _bot.SendMessage(chatId, "Усі фільтри скинуто. Почнемо з вибору кімнат:");
                return await ShowRoomSelection(chatId, state);
            }
            await _bot.SendMessage(
                chatId,
                "⚠️ Будь ласка, скористайтеся кнопками нижче, щоб змінити фільтр."
            );
            return Ok();
        }
    }
}
