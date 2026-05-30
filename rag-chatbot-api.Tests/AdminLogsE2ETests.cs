using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using rag_chatbot_api.Data;
using rag_chatbot_api.Dtos.Auth;

namespace rag_chatbot_api.Tests;

public class AdminLogsE2ETests : IClassFixture<ChatApiFactory>
{
    private readonly ChatApiFactory _factory;

    public AdminLogsE2ETests(ChatApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetLogs_SearchesPersistedSqliteLogs()
    {
        await _factory.ResetDatabaseAsync();

        var client = _factory.CreateClient();
        var auth = await RegisterAndPromoteAdminAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);

        var marker = $"persisted-log-{Guid.NewGuid():N}";
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.ApplicationLogEntries.Add(new rag_chatbot_api.Models.ApplicationLogEntry
            {
                TimestampUtc = DateTime.UtcNow,
                Level = "Information",
                Category = "Tests.AdminLogs",
                Message = $"Persisted admin log marker {marker}",
                EventId = 42,
                RequestPath = $"/api/test/logs/{marker}",
                RequestMethod = "GET",
                UserId = auth.Id.ToString()
            });
            await db.SaveChangesAsync();
        }

        AdminLogQueryResponse? result = null;
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var response = await client.GetAsync($"/api/admin/logs?search={Uri.EscapeDataString(marker)}&level=Information&page=1&pageSize=20");
            response.EnsureSuccessStatusCode();

            result = await response.Content.ReadFromJsonAsync<AdminLogQueryResponse>(new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (result?.Items.Any(item => item.Message.Contains(marker, StringComparison.Ordinal)) == true)
            {
                break;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250));
        }

        Assert.NotNull(result);
        Assert.NotEmpty(result!.Items);

        var entry = result.Items.FirstOrDefault(item => string.Equals(item.RequestPath, $"/api/test/logs/{marker}", StringComparison.Ordinal));
        Assert.NotNull(entry);
        Assert.Equal("Information", entry!.Level);
        Assert.Equal("Tests.AdminLogs", entry.Category);
    }

    private async Task<AuthResponse> RegisterAndPromoteAdminAsync(HttpClient client)
    {
        var email = $"admin-logs-{Guid.NewGuid():N}@example.test";
        const string password = "Passw0rd!";

        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", new
        {
            name = "Admin Logs Tester",
            email,
            password
        });
        registerResponse.EnsureSuccessStatusCode();

        var auth = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.FirstAsync(candidate => candidate.Email == email);
        user.Role = "Admin";
        await db.SaveChangesAsync();

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email,
            password
        });
        loginResponse.EnsureSuccessStatusCode();

        var adminAuth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(adminAuth);
        Assert.Equal("Admin", adminAuth!.Role);

        return adminAuth;
    }

    private sealed class AdminLogQueryResponse
    {
        public IReadOnlyList<AdminLogEntryResponse> Items { get; init; } = [];
        public int TotalCount { get; init; }
        public int Page { get; init; }
        public int PageSize { get; init; }
    }

    private sealed class AdminLogEntryResponse
    {
        public long Id { get; init; }
        public string Level { get; init; } = string.Empty;
        public string Category { get; init; } = string.Empty;
        public string Message { get; init; } = string.Empty;
        public string? RequestPath { get; init; }
    }
}
