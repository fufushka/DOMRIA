using System;
using System.Net.Http;
using System.Threading.Tasks;
using DomRia.Controllers;
using DOMRIA.Handlers;
using DOMRIA.Helpers;
using DOMRIA.Interfaces;
using DOMRIA.Models;
using DOMRIA.Services;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using Moq;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Xunit;

public class TelegramControllerTests
{
    private readonly Mock<IMongoDatabase> _mockDatabase;
    private readonly Mock<ITelegramBotClient> _mockBot;
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
    private readonly TelegramController _controller;
    private readonly Mock<IUserStateService> _mockUserRepo;

    public TelegramControllerTests()
    {
        _mockDatabase = new Mock<IMongoDatabase>();
        _mockBot = new Mock<ITelegramBotClient>();
        _mockHttpClientFactory = new Mock<IHttpClientFactory>();
        _mockHttpClientFactory
            .Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient());
        _mockUserRepo = new Mock<IUserStateService>();
        _mockUserRepo.Setup(s => s.SaveAsync(It.IsAny<UserSearchState>())).ReturnsAsync(true); // ← тепер TrySave буде працювати
        _mockUserRepo
            .Setup(r => r.GetAsync(It.IsAny<long>()))
            .ReturnsAsync(new UserSearchState { UserId = 123, FavoriteFlatIds = new List<int>() });

        var room = new RoomSelectionHandler(_mockHttpClientFactory.Object, _mockBot.Object);
        var district = new DistrictSelectionHandler(_mockHttpClientFactory.Object, _mockBot.Object);
        var budget = new BudgetInputHandler(_mockHttpClientFactory.Object, _mockBot.Object);
        var sort = new SortSelectionHandler(_mockHttpClientFactory.Object, _mockBot.Object);
        var filter = new SelectionFilterChangeHandler(
            _mockHttpClientFactory.Object,
            _mockBot.Object
        );
        var special = new SpecialFilterHandler(_mockHttpClientFactory.Object, _mockBot.Object);
        var start = new CommandStartHandler(_mockHttpClientFactory.Object, _mockBot.Object);
        var stepHelper = new SearchStepHelper(_mockBot.Object, _mockHttpClientFactory.Object);

        var repo = new UserStateService(_mockDatabase.Object);
        var compareHandler = new CompareHandler(_mockHttpClientFactory.Object, _mockBot.Object);
        var userStateHelper = new UserStateHelper(_mockHttpClientFactory.Object, _mockBot.Object);
        var favoritesHandler = new FavoritesHandler(
            _mockHttpClientFactory.Object,
            _mockBot.Object,
            userStateHelper
        );

        _controller = new TelegramController(
            _mockBot.Object,
            _mockUserRepo.Object,
            room,
            district,
            budget,
            sort,
            filter,
            special,
            start,
            compareHandler,
            stepHelper,
            userStateHelper,
            favoritesHandler
        );
    }

    [Fact]
    public async Task Post_ReturnsOk_WhenTextMessageIsHandled()
    {
        var message = (Message)Activator.CreateInstance(typeof(Message), nonPublic: true)!;
        typeof(Message).GetProperty("Chat")!.SetValue(message, new Chat { Id = 123 });
        typeof(Message).GetProperty("From")!.SetValue(message, new User { Id = 123 });
        typeof(Message).GetProperty("Text")!.SetValue(message, "/start");

        var update = (Update)Activator.CreateInstance(typeof(Update), nonPublic: true)!;
        typeof(Update).GetProperty("Message")!.SetValue(update, message);

        var result = await _controller.Post(update);

        Assert.IsAssignableFrom<StatusCodeResult>(result);
        Assert.Equal(200, ((StatusCodeResult)result).StatusCode);
    }

    [Fact]
    public async Task StartSearch_ReturnsOk_WhenTrySaveUserStateSucceeds()
    {
        // Arrange
        var botMock = new Mock<ITelegramBotClient>();
        var httpClientFactoryMock = new Mock<IHttpClientFactory>();
        httpClientFactoryMock
            .Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient());

        var stepHelper = new SearchStepHelper(botMock.Object, httpClientFactoryMock.Object);

        var state = new UserSearchState
        {
            UserId = 123,
            RoomCountOptions = new List<int> { 1 },
            Step = "",
            PreviousStep = "",
        };

        // ✅ Замість реального збереження — фейкова функція яка повертає true
        Func<UserSearchState, long, Task<bool>> trySaveUserState = (s, chatId) =>
            Task.FromResult(true);

        // Act
        var result = await stepHelper.StartSearch(123, state, trySaveUserState);

        // Assert
        Assert.IsType<OkResult>(result);
    }

    [Fact]
    public async Task Post_ReturnsOk_WhenNonTextMessage()
    {
        var message = (Message)Activator.CreateInstance(typeof(Message), nonPublic: true)!;
        typeof(Message).GetProperty("Chat")!.SetValue(message, new Chat { Id = 123 });
        typeof(Message).GetProperty("From")!.SetValue(message, new User { Id = 123 });
        // не встановлюємо Text — це означає не текстове повідомлення

        var update = (Update)Activator.CreateInstance(typeof(Update), nonPublic: true)!;
        typeof(Update).GetProperty("Message")!.SetValue(update, message);

        var result = await _controller.Post(update);

        Assert.IsType<OkResult>(result);
    }

    [Fact]
    public async Task Post_ReturnsBadRequest_WhenUpdateIsNull()
    {
        var result = await _controller.Post(null);

        Assert.IsType<BadRequestResult>(result);
    }
}
