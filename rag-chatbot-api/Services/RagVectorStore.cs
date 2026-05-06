using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Connectors.SqliteVec;
using rag_chatbot_api.Data;

namespace rag_chatbot_api.Services;

internal static class RagVectorStore
{
    public static VectorStoreCollection<string, RagVectorStoreRecord> CreateCollection(
        AppDbContext dbContext,
        string embeddingModelId,
        int dimensions)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentException.ThrowIfNullOrWhiteSpace(embeddingModelId);

        if (dimensions < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(dimensions));
        }

        var store = new SqliteVectorStore(GetConnectionString(dbContext));
        return store.GetCollection<string, RagVectorStoreRecord>(
            GetCollectionName(embeddingModelId),
            CreateDefinition(dimensions));
    }

    public static async Task<bool> CollectionExistsAsync(
        AppDbContext dbContext,
        string embeddingModelId,
        CancellationToken cancellationToken = default)
    {
        var store = new SqliteVectorStore(GetConnectionString(dbContext));
        return await store.CollectionExistsAsync(GetCollectionName(embeddingModelId), cancellationToken);
    }

    public static async Task EnsureCollectionDeletedAsync(
        AppDbContext dbContext,
        string embeddingModelId,
        CancellationToken cancellationToken = default)
    {
        var store = new SqliteVectorStore(GetConnectionString(dbContext));
        var collectionName = GetCollectionName(embeddingModelId);

        if (!await store.CollectionExistsAsync(collectionName, cancellationToken))
        {
            return;
        }

        await store.EnsureCollectionDeletedAsync(collectionName, cancellationToken);
    }

    public static async Task DeleteAsync(
        AppDbContext dbContext,
        string embeddingModelId,
        string key,
        CancellationToken cancellationToken = default)
    {
        if (!await CollectionExistsAsync(dbContext, embeddingModelId, cancellationToken))
        {
            return;
        }

        var collection = CreateCollection(dbContext, embeddingModelId, dimensions: 1);
        await collection.DeleteAsync(key, cancellationToken);
    }

    private static VectorStoreCollectionDefinition CreateDefinition(int dimensions)
    {
        return new VectorStoreCollectionDefinition
        {
            Properties =
            [
                new VectorStoreKeyProperty(nameof(RagVectorStoreRecord.Key), typeof(string)),
                new VectorStoreDataProperty(nameof(RagVectorStoreRecord.Title), typeof(string)),
                new VectorStoreDataProperty(nameof(RagVectorStoreRecord.Url), typeof(string)),
                new VectorStoreDataProperty(nameof(RagVectorStoreRecord.Content), typeof(string)),
                new VectorStoreVectorProperty(nameof(RagVectorStoreRecord.Embedding), typeof(ReadOnlyMemory<float>), dimensions)
                {
                    DistanceFunction = DistanceFunction.CosineDistance
                }
            ]
        };
    }

    private static string GetCollectionName(string embeddingModelId)
    {
        var suffix = new string(
            embeddingModelId
                .Trim()
                .ToLowerInvariant()
                .Select(ch => char.IsLetterOrDigit(ch) ? ch : '_')
                .ToArray())
            .Trim('_');

        return string.IsNullOrWhiteSpace(suffix)
            ? "rag_document_vectors"
            : $"rag_document_vectors_{suffix}";
    }

    private static string GetConnectionString(AppDbContext dbContext)
    {
        var connectionString = dbContext.Database.GetConnectionString();
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            return connectionString;
        }

        connectionString = dbContext.Database.GetDbConnection().ConnectionString;
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            return connectionString;
        }

        throw new InvalidOperationException("The SQLite connection string is not configured.");
    }
}

internal sealed class RagVectorStoreRecord
{
    public string Key { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public ReadOnlyMemory<float> Embedding { get; init; }
}