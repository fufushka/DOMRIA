using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using DOMRIA.Handlers;
using DOMRIA.Helpers;
using DOMRIA.Interfaces;
using DOMRIA.Models;
using DOMRIA.Services;
using Microsoft.AspNetCore.Mvc;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

[ApiController]
[Route("api/[controller]")]
public class TelegramController : ControllerBase
{
    private readonly ITelegramBotClient _bot;
    private readonly HttpClient _httpClient;
    private readonly IUserStateService _userRepo;
    private readonly RoomSelectionHandler _roomHandler;
    private readonly DistrictSelectionHandler _districtHandler;
    private readonly BudgetInputHandler _budgetHandler;
    private readonly SortSelectionHandler _sortHandler;
    private readonly SelectionFilterChangeHandler _filterHandler;
    private readonly SpecialFilterHandler _specialfilterHandler;
    private readonly CommandStartHandler _commandStartHandler;

    private readonly SearchStepHelper _searchStepHelper;

    public TelegramController(
        ITelegramBotClient bot,
        IUserStateService userRepo,
        RoomSelectionHandler roomHandler,
        DistrictSelectionHandler districtHandler,
        BudgetInputHandler budgetHandler,
        SortSelectionHandler sortHandler,
        SelectionFilterChangeHandler filterHandler,
        SpecialFilterHandler specialfilterHandler,
        CommandStartHandler commandStartHandler,
        SearchStepHelper searchStepHelper
    )
    {
        _bot = bot;
        _userRepo = userRepo;
        _httpClient = new HttpClient { BaseAddress = new Uri("https://domria.onrender.com") };
        _roomHandler = roomHandler;
        _districtHandler = districtHandler;
        _budgetHandler = budgetHandler;
        _sortHandler = sortHandler;
        _filterHandler = filterHandler;
        _specialfilterHandler = specialfilterHandler;
        _commandStartHandler = commandStartHandler;

        _searchStepHelper = searchStepHelper;
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] Update update)
    {
        try
        {
            if (update == null)
            {
                Console.WriteLine("❗ Update обʼєкт = null");
                return BadRequest();
            }

            if (update.Type == UpdateType.CallbackQuery && update.CallbackQuery != null)
                return await HandleCallback(update.CallbackQuery);

            if (update.Type == UpdateType.Message && update.Message != null)
            {
                var message = update.Message;

                if (message.Type == MessageType.Text)
                {
                    // 🛡️ Захист від DDoS або шкідливих повідомлень
                    var text = message.Text?.Trim();

                    if (string.IsNullOrWhiteSpace(text) || ContainsSuspiciousInput(text))
                    {
                        await _bot.SendMessage(
                            message.Chat.Id,
                            "⚠️ Я не розумію це повідомлення. Спробуйте використати кнопки нижче."
                        );
                        return Ok();
                    }

                    return await HandleTextMessage(message);
                }

                if (message.Chat != null)
                {
                    await _bot.SendMessage(
                        message.Chat.Id,
                        "⚠️ Я можу обробляти лише текстові повідомлення."
                    );
                }

                return Ok();
            }

            Console.WriteLine("⚠️ Update не є повідомленням або callback.");
            return Ok();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Помилка у TelegramController: {ex.Message}");
            return StatusCode(500); // Telegram отримає 500 і не буде плутанини
        }
    }

    private async Task<IActionResult> HandleCallback(CallbackQuery callback)
    {
        var userId = callback.From.Id;
        var chatId = callback.Message.Chat.Id;
        var data = callback.Data;
        var state = await GetUserState(userId);
        state.FavoriteFlatIds ??= new List<int>();
        state.CompareFlatIds ??= new List<int>();
        if (data.StartsWith("fav_"))
        {
            var flatId = int.Parse(data.Replace("fav_", ""));
            if (!state.FavoriteFlatIds.Contains(flatId))
            {
                state.FavoriteFlatIds.Add(flatId);
                if (!await TrySaveUserState(state, chatId))
                    return Ok();
                await _bot.AnswerCallbackQuery(callback.Id, "Додано в улюблене ❤️");
            }
            else
            {
                await _bot.AnswerCallbackQuery(callback.Id, "Вже в улюбленому ❤️");
            }
        }
        else if (data.StartsWith("unfav_"))
        {
            var flatId = int.Parse(data.Replace("unfav_", ""));
            if (state.FavoriteFlatIds.Contains(flatId))
            {
                state.FavoriteFlatIds.Remove(flatId);
                if (!await TrySaveUserState(state, chatId))
                    return Ok();
                await _bot.DeleteMessage(chatId, callback.Message.MessageId);
                await _bot.AnswerCallbackQuery(callback.Id, "Видалено з улюбленого 💔");
                ;
            }
            else
            {
                await _bot.AnswerCallbackQuery(
                    callback.Id,
                    "Цієї квартири вже немає в улюбленому."
                );
            }
        }
        else if (data.StartsWith("compare_"))
        {
            var command = data.Replace("compare_", "");

            if (command == "reset")
            {
                state.CompareFlatIds ??= new List<int>();
                state.CompareFlatIds.Clear();
                await TrySaveUserState(state, chatId);
                await _bot.DeleteMessage(chatId, callback.Message.MessageId);
                await _bot.AnswerCallbackQuery(callback.Id, "✅ Порівняння очищено");
            }
            else if (int.TryParse(command, out int flatId))
            {
                if (state.CompareFlatIds.Contains(flatId))
                {
                    await _bot.AnswerCallbackQuery(
                        callback.Id,
                        "Ця квартира вже додана до порівняння"
                    );
                }
                else if (state.CompareFlatIds.Count >= 2)
                {
                    await _bot.AnswerCallbackQuery(
                        callback.Id,
                        "❗ Можна порівняти лише 2 квартири"
                    );
                }
                else
                {
                    state.CompareFlatIds ??= new List<int>();
                    state.CompareFlatIds.Add(flatId);
                    if (!await TrySaveUserState(state, chatId))
                        return Ok();
                    await _bot.AnswerCallbackQuery(callback.Id, "✅ Додано до порівняння");
                }
            }
        }
        else if (data == "compare_show")
        {
            return await ShowComparison(chatId, state);
        }
        else if (data == "compare_reset")
        {
            state.CompareFlatIds ??= new List<int>();
            state.CompareFlatIds.Clear();
            await TrySaveUserState(state, chatId);
            await _bot.DeleteMessage(chatId, callback.Message.MessageId);

            await _bot.AnswerCallbackQuery(callback.Id, "✅ Порівняння очищено");
        }

        return Ok();
    }

    private async Task<IActionResult> HandleTextMessage(Message message)
    {
        var chatId = message.Chat.Id;
        var userId = message.From.Id;
        var messageText = message.Text.Trim();
        var state = await _userRepo.GetAsync(userId) ?? new UserSearchState { UserId = userId };

        if (messageText == "/start")
            return await _commandStartHandler.HandleStartCommand(chatId, userId);
        if (messageText == "🔍 Знайти квартиру")
            return await _searchStepHelper.StartSearch(chatId, state, TrySaveUserState);
        if (messageText == "⬅️ Назад" && string.IsNullOrEmpty(state.Step))
        {
            return await _commandStartHandler.HandleStartCommand(chatId, userId);
        }
        switch (state.Step)
        {
            case "rooms":
                return await _roomHandler.HandleRoomSelection(
                    messageText,
                    chatId,
                    state,
                    _searchStepHelper.ShowRoomSelection,
                    _searchStepHelper.ShowDistrictSelection,
                    ShowNextFlats,
                    _searchStepHelper.PromptFilterChange,
                    _commandStartHandler.HandleStartCommand
                );
            case "districts":
                return await _districtHandler.HandleDistrictSelection(
                    messageText,
                    chatId,
                    state,
                    _searchStepHelper.ShowBudgetOptions,
                    ShowNextFlats,
                    _searchStepHelper.ShowDistrictSelection,
                    _searchStepHelper.PromptFilterChange,
                    _searchStepHelper.ShowRoomSelection
                );
            case "budget":
                return await _budgetHandler.HandleBudgetInput(
                    messageText,
                    chatId,
                    state,
                    ShowNextFlats,
                    _searchStepHelper.ShowDistrictSelection,
                    _searchStepHelper.PromptFilterChange,
                    _searchStepHelper.PromptSortSelection,
                    _commandStartHandler.HandleStartCommand
                );
            case "sort_select":
                return await _sortHandler.HandleSortSelection(
                    messageText,
                    chatId,
                    state,
                    ShowNextFlats,
                    _commandStartHandler.HandleStartCommand
                );
            case "done":
                if (messageText.StartsWith("➡️"))
                {
                    if (state.MatchingFlats == null || !state.MatchingFlats.Any())
                    {
                        // 🔧 тут ми виконуємо ПОШУК, якщо квартир ще немає
                        var response = await _httpClient.PostAsJsonAsync("/api/flat/search", state);
                        var result = await response.Content.ReadFromJsonAsync<FlatSearchResponse>();
                        state.MatchingFlats = result?.items ?? new List<int>();
                        state.TotalFlatCount = result?.count ?? 0;
                    }
                    if (state.MatchingFlats == null || !state.MatchingFlats.Any())
                    {
                        await _bot.SendMessage(
                            chatId,
                            "⚠️ Спочатку оберіть фільтри, перш ніж переходити далі.",
                            replyMarkup: _searchStepHelper.GetMainMenuMarkup()
                        );
                        return Ok();
                    }

                    return await ShowNextFlats(chatId, state);
                }
                break;
            case "filter_select":
                return await _filterHandler.HandleFilterChangeSelection(
                    messageText,
                    chatId,
                    state,
                    _searchStepHelper.ShowRoomSelection,
                    _searchStepHelper.ShowDistrictSelection,
                    _searchStepHelper.ShowBudgetOptions
                );
            case "special_filters":
                return await _specialfilterHandler.HandleSpecialFilters(
                    messageText,
                    chatId,
                    state,
                    ShowNextFlats,
                    _searchStepHelper.ShowSpecialFilterOptions,
                    _searchStepHelper.GetMainMenuMarkup
                );
        }

        if (messageText == "💌 Обрані квартири")
        {
            await _bot.DeleteMessage(chatId, message.MessageId);
            return await ShowFavorites(chatId, userId);
        }
        if (messageText == "🔃 Змінити сортування")
        {
            state.Step = "sort_select";
            if (!await TrySaveUserState(state, chatId))
                return Ok();
            return await _searchStepHelper.PromptSortSelection(chatId, state);
        }

        if (messageText == "⚙️ Змінити фільтр пошуку")
        {
            state.Step = "filter_select";
            if (!await TrySaveUserState(state, chatId))
                return Ok();
            return await _searchStepHelper.PromptFilterChange(chatId, state);
        }
        if (messageText == "📝 Спеціальні побажання")
        {
            state.Step = "special_filters";
            if (!await TrySaveUserState(state, chatId))
                return Ok();
            return await _searchStepHelper.ShowSpecialFilterOptions(chatId, state);
        }
        if (messageText == "🆚 Порівняти обрані")
        {
            await _bot.DeleteMessage(chatId, message.MessageId);
            return await ShowComparison(chatId, state);
        }

        var replyMarkup = state.Step == "done" ? _searchStepHelper.GetMainMenuMarkup() : null;
        await _bot.SendMessage(
            chatId,
            "⚠️ Я не розумію цю команду. Будь ласка, скористайтеся кнопками нижче.",
            replyMarkup: replyMarkup
        );

        return Ok();
    }

    private async Task<IActionResult> ShowFavorites(long chatId, long userId)
    {
        var state = await GetUserState(userId);
        if (state.FavoriteFlatIds == null || !state.FavoriteFlatIds.Any())
        {
            await _bot.SendMessage(chatId, "У вас ще немає улюблених квартир.");
            return Ok();
        }

        foreach (var id in state.FavoriteFlatIds)
        {
            var flat = await _httpClient.GetFromJsonAsync<FlatResult>($"/api/flat/{id}");
            if (flat != null)
            {
                await _bot.SendFlatMessage(chatId, flat, state);
            }
        }
        return Ok();
    }

    private async Task<UserSearchState> GetUserState(long userId)
    {
        var result = await _httpClient.GetAsync($"/api/user/{userId}");
        return result.IsSuccessStatusCode
            ? await result.Content.ReadFromJsonAsync<UserSearchState>()
            : new UserSearchState { UserId = userId };
    }

    private async Task<IActionResult> ShowNextFlats(long chatId, UserSearchState state)
    {
        const int limit = 5;

        // Якщо дійшли до кінця поточного списку то завантажуємо нову сторінку
        if (state.CurrentIndex >= state.MatchingFlats.Count)
        {
            var loaded = await TryLoadNextFlatPageAsync(chatId, state);
            if (!loaded)
                return Ok();
        }

        var toShowIds = state.MatchingFlats.Skip(state.CurrentIndex).Take(limit).ToList(); // Після можливого оновлення беремо айдішку для показу

        foreach (var id in toShowIds)
        {
            var flat = await _httpClient.GetFromJsonAsync<FlatResult>($"/api/flat/{id}");

            if (flat == null)
            {
                await _bot.SendMessage(
                    chatId,
                    "⚠️ Не вдалося завантажити інформацію про квартиру."
                );
                continue;
            }

            state.NotifiedFlatIds ??= new();
            if (!state.NotifiedFlatIds.Contains(id))
                state.NotifiedFlatIds.Add(id);

            await _bot.SendFlatMessage(chatId, flat, state);
        }

        state.CurrentIndex += toShowIds.Count;

        if (!await TrySaveUserState(state, chatId))
            return Ok();

        if (state.CurrentIndex < state.MatchingFlats.Count)
        {
            int shown = state.CurrentIndex + (state.CurrentPage * 100);
            int total = state.TotalFlatCount;

            await _bot.SendMessage(
                chatId,
                $"📊 {shown} / {total} квартир переглянуто",
                replyMarkup: _searchStepHelper.GetMainMenuMarkup()
            );
            return Ok();
        }
        else
        {
            // Якщо на цій сторінці всі показані то переходимо до наступної
            return await ShowNextFlats(chatId, state);
        }
    }

    private async Task<bool> TrySaveUserState(UserSearchState state, long chatId)
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
            await _bot.SendMessage(chatId, "⚠️ Виникла помилка при збереженні. Спробуйте пізніше.");
            return false;
        }
    }

    private async Task<IActionResult> ShowComparison(long chatId, UserSearchState state)
    {
        if (state.CompareFlatIds.Count < 1)
        {
            await _bot.SendMessage(
                chatId,
                "У вас не вибрано жодної квартири для порівняння (потрібно 2) 🧐"
            );
            return Ok();
        }
        if (state.CompareFlatIds.Count < 2)
        {
            await _bot.SendMessage(chatId, "Додайте ще одну квартиру для порівняння  🧐");
            return Ok();
        }

        var flat1 = await _httpClient.GetFromJsonAsync<FlatResult>(
            $"/api/flat/{state.CompareFlatIds[0]}"
        );
        var flat2 = await _httpClient.GetFromJsonAsync<FlatResult>(
            $"/api/flat/{state.CompareFlatIds[1]}"
        );

        if (flat1 == null || flat2 == null)
        {
            await _bot.SendMessage(chatId, "❌ Не вдалося завантажити інформацію про квартири.");
            return Ok();
        }

        string msg = $"""
🔎 Порівняння:
 🏠 <a href="{flat1.Url}">{flat1.FlatId}</a>  |  🏠 <a href="{flat2.Url}">{flat2.FlatId}</a>
💰 {flat1.Price}  |  💰 {flat2.Price}
📐 {flat1.Area}   |  📐 {flat2.Area}
🏢 {flat1.FloorInfo} | 🏢 {flat2.FloorInfo}
📍 {flat1.Street} | 📍 {flat2.Street}
🚇 {flat1.MetroStation} | 🚇 {flat2.MetroStation}
🌍 {flat1.AdminDistrict} | 🌍 {flat2.AdminDistrict}
🕒 {flat1.PublishedAt} | 🕒 {flat2.PublishedAt}
🇺🇦 ЄОселя: {(flat1.SupportsYeOselya ? "✅" : "❌")} | {(flat2.SupportsYeOselya ? "✅" : "❌")}
""";

        var buttons = new InlineKeyboardMarkup(
            new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData(
                        "🗑 Очистити порівняння",
                        "compare_reset"
                    ),
                },
            }
        );

        await _bot.SendMessage(
            chatId,
            msg,
            parseMode: Telegram.Bot.Types.Enums.ParseMode.Html,
            replyMarkup: buttons
        );
        return Ok();
    }

    private bool ContainsSuspiciousInput(string input)
    {
        // Занадто довге — можливо, атака
        if (input.Length > 300)
            return true;

        // Заборонені символи (HTML, скрипти, SQL)
        var dangerousPatterns = new[]
        {
            "<script",
            "</script",
            "<",
            ">",
            "--",
            ";",
            "DROP",
            "SELECT",
            "INSERT",
            "UPDATE",
            "DELETE",
            "xp_",
            "exec",
            "union",
            "%",
            "$",
        };

        foreach (var pattern in dangerousPatterns)
        {
            if (input.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }

        // Якщо лише емодзі, або набір випадкових символів (неалфавітних)
        if (input.All(c => !char.IsLetterOrDigit(c)))
            return true;

        return false;
    }

    private async Task<bool> TryLoadNextFlatPageAsync(long chatId, UserSearchState state)
    {
        state.CurrentPage++;
        var response = await _httpClient.PostAsJsonAsync("/api/flat/search", state);
        if (!response.IsSuccessStatusCode)
        {
            await _bot.SendMessage(chatId, "❌ Не вдалося завантажити наступні квартири.");
            return false;
        }

        var searchResult = await response.Content.ReadFromJsonAsync<FlatSearchResponse>();

        if (searchResult?.items == null || !searchResult.items.Any())
        {
            state.Step = null;
            await TrySaveUserState(state, chatId);

            await _bot.SendMessage(chatId, "Це всі квартири за вашими критеріями.");
            await _commandStartHandler.HandleStartCommand(chatId, state.UserId);
            return false;
        }

        // ✅ Успішно оновлюємо стан
        state.MatchingFlats = searchResult.items;
        state.CurrentIndex = 0;
        state.TotalFlatCount = searchResult.count;

        return true;
    }
}
