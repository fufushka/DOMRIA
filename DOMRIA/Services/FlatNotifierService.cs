using System.Net.Http.Json;
using System.Text.Json;
using DOMRIA.Helpers;
using DOMRIA.Models;
using Microsoft.Extensions.Hosting;
using Telegram.Bot;
using Telegram.Bot.Types;

public class FlatNotifierService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly IHttpClientFactory _httpClientFactory;

    public FlatNotifierService(IServiceProvider services, IHttpClientFactory httpClientFactory)
    {
        _services = services;
        _httpClientFactory = httpClientFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var client = _httpClientFactory.CreateClient();
        client.BaseAddress = new Uri("http://localhost");
        await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var users = await client.GetFromJsonAsync<List<UserSearchState>>("/api/user/all");

                foreach (var user in users)
                {
                    var searchResponse = await client.PostAsJsonAsync("/api/flat/search", user);

                    if (!searchResponse.IsSuccessStatusCode)
                    {
                        var error = await searchResponse.Content.ReadAsStringAsync();

                        continue;
                    }

                    var content = await searchResponse.Content.ReadAsStringAsync();

                    FlatSearchResponse? searchResult = null;
                    try
                    {
                        searchResult = JsonSerializer.Deserialize<FlatSearchResponse>(content);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"❌ JSON parsing error: {ex.Message}");
                        continue;
                    }

                    var flatIds = searchResult?.items ?? new List<int>();
                    if (flatIds == null || flatIds.Count == 0)
                        continue;

                    user.NotifiedFlatIds ??= new List<int>();
                    var trulyNewFlats = flatIds.Except(user.NotifiedFlatIds).Take(1).ToList();

                    if (trulyNewFlats.Count == 0)
                    {
                        continue;
                    }

                    Console.WriteLine(
                        $"📬 Надсилаємо {trulyNewFlats.Count} нових квартир для {user.UserId}"
                    );

                    using var scope = _services.CreateScope();
                    var bot = scope.ServiceProvider.GetRequiredService<ITelegramBotClient>();

                    bool userBlocked = false;

                    foreach (var flatId in trulyNewFlats)
                    {
                        var flat = await client.GetFromJsonAsync<FlatResult>($"/api/flat/{flatId}");
                        if (flat == null)
                            continue;

                        try
                        {
                            await bot.SendFlatMessage(user.UserId, flat, user, true);
                        }
                        catch (Telegram.Bot.Exceptions.ApiRequestException ex)
                            when (ex.Message.Contains("bot was blocked"))
                        {
                            Console.WriteLine(
                                $"❌ Користувач {user.UserId} заблокував бота. Видаляємо з бази."
                            );
                            var deleteResponse = await client.DeleteAsync(
                                $"/api/user/{user.UserId}"
                            );
                            if (deleteResponse.IsSuccessStatusCode)
                                Console.WriteLine($"✅ Користувач {user.UserId} видалений з бази.");
                            else
                                Console.WriteLine(
                                    $"⚠️ Не вдалося видалити user {user.UserId}: {deleteResponse.StatusCode}"
                                );

                            userBlocked = true;
                            break;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(
                                $"❌ Помилка надсилання повідомлення користувачу {user.UserId}: {ex.Message}"
                            );
                        }
                    }

                    if (!userBlocked)
                    {
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
