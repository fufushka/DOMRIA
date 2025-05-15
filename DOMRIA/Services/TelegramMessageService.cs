using DOMRIA.Interfaces;
using DOMRIA.Models;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;

namespace DOMRIA.Services
{
    public class TelegramMessageService : IMessageService
    {
        private readonly ITelegramBotClient _bot;

        public TelegramMessageService(ITelegramBotClient bot)
        {
            _bot = bot;
        }

        public async Task SendTextAsync(
            long chatId,
            string text,
            InlineKeyboardMarkup? markup = null,
            CancellationToken ct = default
        )
        {
            await _bot.SendMessage(chatId, text, replyMarkup: markup, cancellationToken: ct);
        }

        public async Task SendFlatDetailsAsync(
            long chatId,
            FlatResult flat,
            UserSearchState state,
            CancellationToken ct = default
        )
        {
            var isFav = state.FavoriteFlatIds.Contains(flat.FlatId);
            var favButton = isFav
                ? InlineKeyboardButton.WithCallbackData(
                    "💔 Видалити з улюбленого",
                    $"unfav_{flat.FlatId}"
                )
                : InlineKeyboardButton.WithCallbackData(
                    "❤️ Додати в улюблене",
                    $"fav_{flat.FlatId}"
                );

            var markup = new InlineKeyboardMarkup(
                new[]
                {
                    new[] { favButton },
                    new[]
                    {
                        InlineKeyboardButton.WithUrl(
                            "📍 Показати на мапі",
                            $"https://maps.google.com/?q={flat.latitude},{flat.longitude}"
                        ),
                    },
                }
            );

            var lines = new[]
            {
                $"🏠 {flat.Title}",
                $"💰 {flat.Price:N0} ₴",
                $"📐 Площа: {flat.Area} м²",
                $"🚇 Метро: {flat.MetroStation}",
                $"🏢 ЖК: {flat.HousingComplex}",
                $"📍 Адреса: {flat.Street}",
                $"🌍 Район: {flat.AdminDistrict}, {flat.CityDistrict}",
                $"🏗️ Поверх: {flat.FloorInfo}",
                $"🕒 Опубліковано: {flat.PublishedAt:dd.MM.yyyy}",
            };
            var msg = string.Join(Environment.NewLine, lines);

            await SendTextAsync(chatId, msg, markup, ct);
        }
    }
}
