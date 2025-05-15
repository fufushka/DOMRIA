using System.Text.Json;
using DomRia.Config;
using DOMRIA.Config;
using DOMRIA.Handlers;
using DOMRIA.Interfaces;
using DOMRIA.Services;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Telegram.Bot;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

////User State
builder.Services.AddSingleton<Dictionary<long, UserSearchState>>();

///////
//////BotSet////////
builder.Services.Configure<BotSettings>(builder.Configuration.GetSection("BotSettings"));
builder.Services.AddScoped<ITelegramBotClient>(sp =>
{
    var config = sp.GetRequiredService<IOptions<BotSettings>>(); // конфіг з токеном
    return new TelegramBotClient(config.Value.TelegramToken); // створюємо Telegram Bot AP
});

//////////////////////

builder.Services.AddHttpClient(
    "BotClient",
    client =>
    {
        client.BaseAddress = new Uri("http://webapp:80"); // ← твоя API-адреса
    }
);

builder.Services.Configure<MongoDbSettings>(builder.Configuration.GetSection("MongoDb"));

builder.Services.AddSingleton<IMongoClient>(sp =>
{
    var settings = sp.GetRequiredService<IOptions<MongoDbSettings>>().Value;
    return new MongoClient(settings.ConnectionString);
}); // підключення до MongoDB

//////Mongo////
builder.Services.AddScoped(serviceProvider =>
{
    var settings = serviceProvider.GetRequiredService<IOptions<MongoDbSettings>>().Value;
    var client = serviceProvider.GetRequiredService<IMongoClient>();
    return client.GetDatabase(settings.DatabaseName);
}); // отримаємо базу по імені
builder.Services.AddScoped<UserStateService>(); //  сервіс для роботи з MongoDB

/////
builder.Services.AddHttpClient();
builder.Services.AddControllers();

///////////////////Services////////////////////////////
builder.Services.AddScoped<DomRiaService>();
builder.Services.AddScoped<IMessageService, TelegramMessageService>();

builder.Services.AddHostedService<FlatNotifierService>();

/////////////////////////////////////////////////////////
///////////////////Handlers////////////////////////////
builder.Services.AddScoped<RoomSelectionHandler>();
builder.Services.AddScoped<DistrictSelectionHandler>();
builder.Services.AddScoped<BudgetInputHandler>();
builder.Services.AddScoped<SortSelectionHandler>();
builder.Services.AddScoped<SelectionFilterChangeHandler>();
builder.Services.AddScoped<SpecialFilterHandler>();

//////////////////////////////////////////////////////
var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    var config = app.Services.GetRequiredService<IOptions<BotSettings>>().Value;
    var botClient = new TelegramBotClient(config.TelegramToken);

    var webhookUrl = Environment.GetEnvironmentVariable("Webhook__Url");
    if (!string.IsNullOrEmpty(webhookUrl))
    {
        Console.WriteLine($"🔗 Регіструємо webhook: {webhookUrl}");
        await botClient.SetWebhook(webhookUrl);
    }
    else
    {
        Console.WriteLine("⚠️ Webhook__Url не вказано");
    }
}
app.UseRouting();
app.MapControllers();
app.UseSwagger(); // 🧩 генерує Swagger JSON
app.UseSwaggerUI(); // 💡 відображає Swagger UI

app.UseEndpoints(endpoints =>
{
    endpoints.MapControllers();
});
app.Urls.Add("http://*:80");
app.Run();
