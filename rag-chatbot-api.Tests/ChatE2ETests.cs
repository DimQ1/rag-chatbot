using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using rag_chatbot_api.Data;
using rag_chatbot_api.Dtos.Auth;
using rag_chatbot_api.Dtos.ChatSession;
using rag_chatbot_api.Dtos.Rag;
using rag_chatbot_api.Models;
using rag_chatbot_api.Services;

namespace rag_chatbot_api.Tests;

public class ChatE2ETests : IClassFixture<ChatApiFactory>
{
    private readonly ChatApiFactory _factory;

    public ChatE2ETests(ChatApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreateSession_AddMessageAndGetDetail_ReturnsAssistantAnswerWithSources()
    {
        await _factory.ResetDatabaseAsync();

        await _factory.SeedDocumentAsync(new RagSourceDocument
        {
            DocumentId = "test-doc-1",
            Title = "Test Knowledge",
            OriginalFileName = "test-knowledge.md",
            Content = "The answer is 42 for integration test questions.",
            CreatedAtUtc = DateTime.UtcNow,
            SourceUpdatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });

        var client = _factory.CreateClient();

        var registerRequest = new
        {
            name = "Chat Tester",
            email = $"chat-e2e-{Guid.NewGuid():N}@example.test",
            password = "Passw0rd!"
        };

        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", registerRequest);
        registerResponse.EnsureSuccessStatusCode();

        var auth = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);
        Assert.False(string.IsNullOrWhiteSpace(auth.Token));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);

        var createSessionResponse = await client.PostAsJsonAsync("/api/chatsession/create", new { });
        createSessionResponse.EnsureSuccessStatusCode();

        var createdSession = await createSessionResponse.Content.ReadFromJsonAsync<ChatSessionResponse>();
        Assert.NotNull(createdSession);

        var addMessageResponse = await client.PostAsJsonAsync($"/api/chatsession/{createdSession.Id}/add-message", new
        {
            question = "What is the integration test answer?"
        });

        addMessageResponse.EnsureSuccessStatusCode();

        var detailResponse = await client.GetAsync($"/api/chatsession/{createdSession.Id}");
        detailResponse.EnsureSuccessStatusCode();

        var detail = await detailResponse.Content.ReadFromJsonAsync<ChatSessionDetailResponse>(new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.NotNull(detail);
        Assert.Equal(2, detail.Messages.Count);

        var userMessage = detail.Messages[0];
        var assistantMessage = detail.Messages[1];

        Assert.Equal("user", userMessage.Role);
        Assert.Contains("integration test answer", userMessage.Content, StringComparison.OrdinalIgnoreCase);

        Assert.Equal("assistant", assistantMessage.Role);
        Assert.Contains("42", assistantMessage.Content, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(assistantMessage.Sources);
        Assert.NotEmpty(assistantMessage.Sources);
        Assert.Contains(assistantMessage.Sources!, source => source.Title == "Test Knowledge");
    }
}

public sealed class ChatApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"chat-e2e-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.AddDbContext<AppDbContext>(options => options.UseSqlite($"Data Source={_dbPath}"));

            services.RemoveAll<IRagService>();
            services.AddScoped<IRagService, TestRagService>();
        });
    }

    public async Task ResetDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();
    }

    public async Task SeedDocumentAsync(RagSourceDocument document)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        db.RagSourceDocuments.Add(document);
        await db.SaveChangesAsync();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        TryDeleteFile(_dbPath);
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        TryDeleteFile(_dbPath);
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Ignore cleanup failures in test teardown.
        }
    }
}

public sealed class TestRagService : IRagService
{
    private readonly AppDbContext _dbContext;

    public TestRagService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<RagQueryResponse> QueryAsync(string question, CancellationToken cancellationToken = default)
    {
        var source = await _dbContext.RagSourceDocuments
            .AsNoTracking()
            .OrderByDescending(d => d.UpdatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (source is null)
        {
            return new RagQueryResponse
            {
                Answer = "No knowledge documents available.",
                Sources = []
            };
        }

        return new RagQueryResponse
        {
            Answer = $"Based on test knowledge: {source.Content}",
            Sources =
            [
                new RagSource
                {
                    Title = source.Title,
                    Url = $"local://knowledge/{source.OriginalFileName}"
                }
            ]
        };
    }
}
