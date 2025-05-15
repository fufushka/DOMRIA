using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using DOMRIA.Handlers;
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
    private readonly UserStateService _userRepo;
    private readonly RoomSelectionHandler _roomHandler;
    private readonly DistrictSelectionHandler _districtHandler;
    private readonly BudgetInputHandler _budgetHandler;
    private readonly SortSelectionHandler _sortHandler;
    private readonly SelectionFilterChangeHandler _filterHandler;
    private readonly SpecialFilterHandler _specialfilterHandler;

    public TelegramController(
        ITelegramBotClient bot,
        UserStateService userRepo,
        RoomSelectionHandler roomHandler,
        DistrictSelectionHandler districtHandler,
        BudgetInputHandler budgetHandler,
        SortSelectionHandler sortHandler,
        SelectionFilterChangeHandler filterHandler,
        SpecialFilterHandler specialfilterHandler
    )
    {
        _bot = bot;
        _userRepo = userRepo;
        _httpClient = new HttpClient { BaseAddress = new Uri("http://webapp:80") };
        _roomHandler = roomHandler;
        _districtHandler = districtHandler;
        _budgetHandler = budgetHandler;
        _sortHandler = sortHandler;
        _filterHandler = filterHandler;
        _specialfilterHandler = specialfilterHandler;
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] Update update)
    {
        try
        {
            if (update.Type == UpdateType.CallbackQuery && update.CallbackQuery != null)
                return await HandleCallback(update.CallbackQuery);
            if (update.Type == UpdateType.Message)
            {
                var message = update.Message;
                if (message?.Type == MessageType.Text)
                    return await HandleTextMessage(message);
                else
                {
                    var chatId = message.Chat.Id;
                    await _bot.SendMessage(
                        chatId,
                        "⚠️ Я можу обробляти лише текстові повідомлення."
                    );
                    return Ok();
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Помилка у TelegramController: {ex.Message}");
        }
        return Ok();
    }

    private async Task<IActionResult> HandleCallback(CallbackQuery callback)
    {
        var userId = callback.From.Id;
        var chatId = callback.Message.Chat.Id;
        var data = callback.Data;
        var state = await GetUserState(userId);

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
            return await HandleStartCommand(chatId, userId);
        if (messageText == "🔍 Знайти квартиру")
            return await StartSearch(chatId, state);
        if (messageText == "⬅️ Назад" && string.IsNullOrEmpty(state.Step))
        {
            return await HandleStartCommand(chatId, userId);
        }
        switch (state.Step)
        {
            case "rooms":
                return await _roomHandler.HandleRoomSelection(
                    messageText,
                    chatId,
                    state,
                    ShowRoomSelection,
                    ShowDistrictSelection,
                    ShowNextFlats,
                    PromptFilterChange
                );
            case "districts":
                return await _districtHandler.HandleDistrictSelection(
                    messageText,
                    chatId,
                    state,
                    ShowBudgetOptions,
                    ShowNextFlats,
                    ShowDistrictSelection,
                    PromptFilterChange,
                    ShowRoomSelection
                );
            case "budget":
                return await _budgetHandler.HandleBudgetInput(
                    messageText,
                    chatId,
                    state,
                    ShowNextFlats,
                    ShowDistrictSelection,
                    PromptFilterChange,
                    PromptSortSelection
                );
            case "sort_select":
                return await _sortHandler.HandleSortSelection(
                    messageText,
                    chatId,
                    state,
                    ShowNextFlats,
                    HandleStartCommand
                );
            case "done":
                if (messageText.StartsWith("➡️"))
                {
                    if (state.MatchingFlats == null || !state.MatchingFlats.Any())
                    {
                        await _bot.SendMessage(
                            chatId,
                            "⚠️ Спочатку оберіть фільтри, перш ніж переходити далі.",
                            replyMarkup: GetMainMenuMarkup()
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
                    ShowRoomSelection,
                    ShowDistrictSelection,
                    ShowBudgetOptions
                );
            case "special_filters":
                return await _specialfilterHandler.HandleSpecialFilters(
                    messageText,
                    chatId,
                    state,
                    ShowNextFlats,
                    ShowSpecialFilterOptions,
                    GetMainMenuMarkup
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
            return await PromptSortSelection(chatId, state);
        }

        if (messageText == "⚙️ Змінити фільтр пошуку")
        {
            state.Step = "filter_select";
            if (!await TrySaveUserState(state, chatId))
                return Ok();
            return await PromptFilterChange(chatId, state);
        }
        if (messageText == "📝 Спеціальні побажання")
        {
            state.Step = "special_filters";
            if (!await TrySaveUserState(state, chatId))
                return Ok();
            return await ShowSpecialFilterOptions(chatId, state);
        }
        if (messageText == "🆚 Порівняти обрані")
        {
            await _bot.DeleteMessage(chatId, message.MessageId);
            return await ShowComparison(chatId, state);
        }

        var replyMarkup = state.Step == "done" ? GetMainMenuMarkup() : null;
        await _bot.SendMessage(
            chatId,
            "⚠️ Я не розумію цю команду. Будь ласка, скористайтеся кнопками нижче.",
            replyMarkup: replyMarkup
        );

        return Ok();
    }

    private async Task<IActionResult> HandleStartCommand(long chatId, long userId)
    {
        var state = new UserSearchState { UserId = userId };
        if (!await TrySaveUserState(state, chatId))
            return Ok();

        var keyboard = new ReplyKeyboardMarkup(
            new[]
            {
                new[] { new KeyboardButton("🔍 Знайти квартиру") },
                new[] { new KeyboardButton("💌 Обрані квартири") },
            }
        )
        {
            ResizeKeyboard = true,
        };

        await _bot.SendMessage(chatId, "Привіт! Обери дію:", replyMarkup: keyboard);
        return Ok();
    }

    private async Task<IActionResult> StartSearch(long chatId, UserSearchState state)
    {
        state.Step = "rooms";
        state.RoomCountOptions.Clear();
        if (!await TrySaveUserState(state, chatId))
            return Ok();
        return await ShowRoomSelection(chatId, state);
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
                var msg = $"""

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

                bool isFav = state.FavoriteFlatIds.Contains(id);
                var favButtonText = isFav ? "💔 Видалити з улюбленого" : "❤️ Додати в улюблене";
                var favButtonCallback = isFav ? $"unfav_{id}" : $"fav_{id}";

                var buttons = new List<InlineKeyboardButton[]>
                {
                    new[]
                    {
                        InlineKeyboardButton.WithCallbackData(favButtonText, favButtonCallback),
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
                await _bot.SendMessage(chatId, msg, replyMarkup: new InlineKeyboardMarkup(buttons));
            }
        }
        return Ok();
    }

    private async Task<IActionResult> PromptSortSelection(long chatId, UserSearchState state)
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
        return Ok();
    }

    private async Task<IActionResult> PromptFilterChange(long chatId, UserSearchState state)
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
        return Ok();
    }

    private async Task<UserSearchState> GetUserState(long userId)
    {
        var result = await _httpClient.GetAsync($"/api/user/{userId}");
        return result.IsSuccessStatusCode
            ? await result.Content.ReadFromJsonAsync<UserSearchState>()
            : new UserSearchState { UserId = userId };
    }

    private async Task<IActionResult> ShowRoomSelection(long chatId, UserSearchState state)
    {
        var buttons = Enumerable
            .Range(1, 4)
            .Select(n => new KeyboardButton((state.RoomCountOptions.Contains(n) ? "✅ " : "") + n))
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
        return Ok();
    }

    private async Task<IActionResult> ShowDistrictSelection(long chatId, UserSearchState state)
    {
        if (state.AvailableDistricts.Count == 0)
        {
            state.AvailableDistricts = await _httpClient.GetFromJsonAsync<List<DistrictDto>>(
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
        return Ok();
    }

    private async Task<IActionResult> ShowBudgetOptions(long chatId)
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
        return Ok();
    }

    private async Task<IActionResult> ShowSpecialFilterOptions(long chatId, UserSearchState state)
    {
        var buttons = new List<KeyboardButton[]>
        {
            new[] { new KeyboardButton((state.OnlyYeOselya ? "✅ " : "") + "🇺🇦 ЄОселя") },
            new[] { new KeyboardButton((state.NotFirstFloor ? "✅ " : "") + "🧱 Не перший поверх") },
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
        return Ok();
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

    private async Task<IActionResult> ShowNextFlats(long chatId, UserSearchState state)
    {
        const int limit = 5;
        var toShowIds = state.MatchingFlats.Skip(state.CurrentIndex).Take(limit).ToList();

        if (!toShowIds.Any())
        {
            state.CurrentPage++;
            var response = await _httpClient.PostAsJsonAsync("/api/flat/search", state);
            var searchResult = await response.Content.ReadFromJsonAsync<FlatSearchResponse>();

            if (searchResult?.items != null && searchResult.items.Count > 0)
            {
                state.MatchingFlats.AddRange(searchResult.items);
                state.TotalFlatCount = searchResult.count;

                toShowIds = state.MatchingFlats.Skip(state.CurrentIndex).Take(limit).ToList();
            }
            else
            {
                await _bot.SendMessage(chatId, "Це всі квартири за вашими критеріями.");
                return Ok();
            }
        }

        foreach (var id in toShowIds)
        {
            var flat = await _httpClient.GetFromJsonAsync<FlatResult>($"/api/flat/{id}");
            if (flat != null)
            {
                state.NotifiedFlatIds ??= new();
                if (!state.NotifiedFlatIds.Contains(id))
                    state.NotifiedFlatIds.Add(id);

                var msg = $"""
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

                bool isFav = state.FavoriteFlatIds.Contains(id);
                var favButtonText = isFav ? "💔 Видалити з улюбленого" : "❤️ Додати в улюблене";
                var favButtonCallback = isFav ? $"unfav_{id}" : $"fav_{id}";

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

                await _bot.SendMessage(chatId, msg, replyMarkup: new InlineKeyboardMarkup(buttons));
            }
        }

        state.CurrentIndex += toShowIds.Count;

        if (!await TrySaveUserState(state, chatId))
            return Ok();

        if (state.CurrentIndex < state.MatchingFlats.Count)
        {
            int shown = state.CurrentIndex;
            int total = state.TotalFlatCount;

            await _bot.SendMessage(
                chatId,
                $"📊 {shown} / {total} квартир переглянуто",
                replyMarkup: GetMainMenuMarkup()
            );
            return Ok();
        }
        else
        {
            state.Step = null;
            await TrySaveUserState(state, chatId);
            var markup = new ReplyKeyboardMarkup(
                new[]
                {
                    new[] { new KeyboardButton("💌 Обрані квартири") },
                    new[] { new KeyboardButton("🔍 Знайти квартиру") },
                }
            )
            {
                ResizeKeyboard = true,
            };

            await _bot.SendMessage(
                chatId,
                "Це всі квартири за вашими критеріями.",
                replyMarkup: markup
            );
            return Ok();
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
        if (state.CompareFlatIds.Count < 2)
        {
            await _bot.SendMessage(chatId, "Додайте ще одну квартиру для порівняння 🧐");
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
}
