using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using rag_chatbot_api.Data;
using rag_chatbot_api.Models;
using rag_chatbot_api.Options;
using rag_chatbot_api.Services;

namespace rag_chatbot_api.Tests;

public class RagServiceLiveTests
{
    private const string DefaultBaseUrl = "http://127.0.0.1:1234/v1";
    private const string DefaultChatModel = "google/gemma-4-e2b";
    private const string DefaultEmbeddingModel = "text-embedding-bge-m3-embeddings";

    [Fact]
    public async Task QueryAsync_ReturnsAnswerAndSources_WithLocalOpenAICompatibleEndpoint()
    {
        var apiKey = Environment.GetEnvironmentVariable("RAG_TEST_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return;
        }

        var baseUrl = GetEnvironmentVariableOrDefault("RAG_TEST_BASE_URL", DefaultBaseUrl);
        var chatModel = GetEnvironmentVariableOrDefault("RAG_TEST_CHAT_MODEL", DefaultChatModel);
        var embeddingModel = GetEnvironmentVariableOrDefault("RAG_TEST_EMBED_MODEL", DefaultEmbeddingModel);

        var tempDbPath = Path.Combine(Path.GetTempPath(), $"rag-live-tests-{Guid.NewGuid():N}.db");
        var tempContentRoot = Path.Combine(Path.GetTempPath(), $"rag-live-tests-content-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(tempContentRoot, "KnowledgeBase"));

        try
        {
            await using var dbContext = CreateDbContext(tempDbPath);
            await dbContext.Database.EnsureCreatedAsync();

            dbContext.RagRuntimeConfigurations.Add(new RagRuntimeConfiguration
            {
                Id = 1,
                OpenAIBaseUrl = baseUrl,
                ModelId = chatModel,
                EmbeddingModelId = embeddingModel,
                OpenAIApiKey = apiKey!,
                TopK = 3,
                UpdatedAtUtc = DateTime.UtcNow
            });

            dbContext.RagSourceDocuments.Add(new RagSourceDocument
            {
                DocumentId = "three-little-pigs",
                Title = "The Three Little Pigs",
                OriginalFileName = "the-three-little-pigs.md",
                Content = "The third little pig built a house of bricks. The wolf could not blow it down.",
                CreatedAtUtc = DateTime.UtcNow,
                SourceUpdatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });

            await dbContext.SaveChangesAsync();

            using var loggerFactory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Warning));
            var ragOptions = Microsoft.Extensions.Options.Options.Create(new RagOptions
            {
                OpenAIBaseUrl = baseUrl,
                ModelId = chatModel,
                EmbeddingModelId = embeddingModel,
                OpenAIApiKey = apiKey!,
                TopK = 3
            });

            var indexService = new RagIndexService(
                dbContext,
                ragOptions,
                new TestHostEnvironment(tempContentRoot),
                loggerFactory.CreateLogger<RagIndexService>());

            var reindexResult = await indexService.ReprocessAllAsync();
            Assert.True(reindexResult.ProcessedCount > 0);

            var ragService = new RagService(
                dbContext,
                ragOptions,
                loggerFactory.CreateLogger<RagService>());

            var response = await ragService.QueryAsync("Who built the house of bricks?", cancellationToken: default);

            Assert.False(string.IsNullOrWhiteSpace(response.Answer));
            Assert.NotEmpty(response.Sources);
            Assert.Contains(response.Sources, source => source.Title.Contains("Three Little Pigs", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            TryDeleteFile(tempDbPath);
            TryDeleteDirectory(tempContentRoot);
        }
    }

    private static AppDbContext CreateDbContext(string databasePath)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={databasePath}")
            .Options;

        return new AppDbContext(options);
    }

    private static string GetEnvironmentVariableOrDefault(string name, string fallback)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
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

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup failures in test teardown.
        }
    }

    private sealed class TestHostEnvironment(string contentRootPath) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "rag-chatbot-api.Tests";
        public string ContentRootPath { get; set; } = contentRootPath;
        public IFileProvider ContentRootFileProvider { get; set; } = new PhysicalFileProvider(contentRootPath);
    }

    private sealed class TestAgentSessionStateStore : IAgentSessionStateStore
    {
        private readonly Dictionary<Guid, string> _store = [];

        public Task<string?> LoadAsync(Guid chatSessionId, CancellationToken cancellationToken = default)
        {
            _store.TryGetValue(chatSessionId, out var serializedSession);
            return Task.FromResult(serializedSession);
        }

        public Task SaveAsync(Guid chatSessionId, string serializedSession, CancellationToken cancellationToken = default)
        {
            _store[chatSessionId] = serializedSession;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(Guid chatSessionId, CancellationToken cancellationToken = default)
        {
            _store.Remove(chatSessionId);
            return Task.CompletedTask;
        }
    }
}
