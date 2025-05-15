// Services/Telegram/IMessageService.cs
using System.Threading;
using System.Threading.Tasks;
using DOMRIA.Models;
using Telegram.Bot.Types.ReplyMarkups;

namespace DOMRIA.Interfaces
{
    public interface IMessageService
    {
        Task SendTextAsync(
            long chatId,
            string text,
            InlineKeyboardMarkup? markup = null,
            CancellationToken ct = default
        );
        Task SendFlatDetailsAsync(
            long chatId,
            FlatResult flat,
            UserSearchState state,
            CancellationToken ct = default
        );
    }
}
