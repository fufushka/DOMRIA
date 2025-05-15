using System.Net.Http.Json;
using System.Text.Json;
using DOMRIA.Models;
using Microsoft.Extensions.Hosting;
using Telegram.Bot;

public class FlatNotifierService : BackgroundService
{
    private readonly IHttpClientFactory _httpClientFactory;

    private readonly IServiceProvider _services;

    public FlatNotifierService(IHttpClientFactory httpClientFactory, IServiceProvider services)
    {
        _httpClientFactory = httpClientFactory;
        _services = services;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var client = _httpClientFactory.CreateClient();
        client.BaseAddress = new Uri("http://localhost");
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var users = await client.GetFromJsonAsync<List<UserSearchState>>("/api/user/all");

                foreach (var user in users)
                {
                    var searchResponse = await client.PostAsJsonAsync("/api/flat/search", user);
                    var searchResult =
                        await searchResponse.Content.ReadFromJsonAsync<FlatSearchResponse>();

                    var flatIds = searchResult?.items ?? new List<int>();
                    if (flatIds == null || flatIds.Count == 0)
                        continue;

                    user.NotifiedFlatIds ??= new List<int>();
                    var trulyNewFlats = flatIds.Except(user.NotifiedFlatIds).Take(1).ToList();

                    if (trulyNewFlats.Count == 0)
                    {
                        Console.WriteLine($"📭 Немає нових квартир для user {user.UserId}");
                        continue;
                    }

                    Console.WriteLine(
                        $"📬 Надсилаємо {trulyNewFlats.Count} нових квартир для {user.UserId}"
                    );

                    using var scope = _services.CreateScope();
                    var bot = scope.ServiceProvider.GetRequiredService<ITelegramBotClient>();

                    foreach (var flatId in trulyNewFlats)
                    {
                        var flat = await client.GetFromJsonAsync<FlatResult>($"/api/flat/{flatId}");
                        if (flat == null)
                            continue;

                        var msg = $"""
Дивись! З'явився новий варіант для тебе:
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

                        try
                        {
                            await bot.SendMessage(user.UserId, msg);
                        }
                        catch (Telegram.Bot.Exceptions.ApiRequestException ex)
                            when (ex.Message.Contains("bot was blocked"))
                        {
                            Console.WriteLine(
                                $"❌ Користувач {user.UserId} заблокував бота. Видаляємо з бази."
                            );
                            await client.DeleteAsync($"/api/user/{user.UserId}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(
                                $"❌ Помилка надсилання повідомлення користувачу {user.UserId}: {ex.Message}"
                            );
                        }

                        // ✅ додаємо лише нові, не дублюючи
                        user.NotifiedFlatIds.AddRange(trulyNewFlats);
                        await client.PostAsJsonAsync("/api/user", user);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FlatNotifierService] ❌ {ex.Message}");
            }

            await Task.Delay(TimeSpan.FromMinutes(720), stoppingToken);
        }
    }
}
