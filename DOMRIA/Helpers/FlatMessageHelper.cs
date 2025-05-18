using System.Text;
using DOMRIA.Models;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;

namespace DOMRIA.Helpers
{
    public static class FlatMessageHelper
    {
        public static async Task SendFlatMessage(
            this ITelegramBotClient bot,
            long chatId,
            FlatResult flat,
            UserSearchState state,
            bool showHeader = false
        )
        {
            string msg = $"""
{(showHeader ? "👀 Дивись! З'явився новий варіант для тебе:\n\n" : "")}
🏠 {flat.Title}
💰 {flat.Price}
📍 {flat.Url}
📐 Площа: {flat.Area}
🚇 Метро: {flat.MetroStation}
🏢 ЖК: {flat.HousingComplex}
📍 Адреса: {flat.Street}
🌍 Район: {flat.AdminDistrict}, {flat.CityDistrict}
🏗️ Поверх: {flat.FloorInfo}
🕒 Опубліковано: {flat.PublishedAt}
🇺🇦 Підтримка єОселя: {(flat.SupportsYeOselya ? "✅" : "❌")}
""";

            bool isFav = state.FavoriteFlatIds.Contains(flat.FlatId);
            var favButtonText = isFav ? "💔 Видалити з улюбленого" : "❤️ Додати в улюблене";
            var favButtonCallback = isFav ? $"unfav_{flat.FlatId}" : $"fav_{flat.FlatId}";

            var buttons = new List<InlineKeyboardButton[]>
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData(favButtonText, favButtonCallback),
                    InlineKeyboardButton.WithCallbackData(
                        "🆚 Додати до порівняння",
                        $"compare_{flat.FlatId}"
                    ),
                },
            };

            if (flat.latitude.HasValue && flat.longitude.HasValue)
            {
                buttons.Add(
                    new[]
                    {
                        InlineKeyboardButton.WithUrl(
                            "📍 Показати на мапі",
                            $"https://www.google.com/maps?q={flat.latitude},{flat.longitude}"
                        ),
                    }
                );
            }

            await bot.SendMessage(chatId, msg, replyMarkup: new InlineKeyboardMarkup(buttons));
        }
    }
}
