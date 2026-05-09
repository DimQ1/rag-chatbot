using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using rag_chatbot_api.Data;
using rag_chatbot_api.Models;
using rag_chatbot_api.Options;

#pragma warning disable SKEXP0001

namespace rag_chatbot_api.Services;

public class RagIndexService(
    AppDbContext dbContext,
    IOptions<RagOptions> ragOptions,
    IHostEnvironment hostEnvironment,
    ILogger<RagIndexService> logger) : IRagIndexService
{
    private readonly AppDbContext _dbContext = dbContext;
    private readonly RagOptions _ragOptions = ragOptions.Value;
    private readonly string _knowledgeBasePath = Path.Combine(hostEnvironment.ContentRootPath, "KnowledgeBase");
    private readonly ILogger<RagIndexService> _logger = logger;

    public async Task<(int ProcessedCount, int RemovedCount)> ReprocessAllAsync(CancellationToken cancellationToken = default)
    {
        await ImportLegacyKnowledgeBaseFilesAsync(cancellationToken);

        var configuration = await ResolveConfigurationAsync(cancellationToken);
        var kernel = KernelFactory.CreateKernel(configuration, out _);

        await RagVectorStore.EnsureCollectionDeletedAsync(_dbContext, configuration.EmbeddingModelId, cancellationToken);

        var processed = 0;
        var activeDocumentIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var sourceDocuments = await _dbContext.RagSourceDocuments
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        foreach (var sourceDocument in sourceDocuments)
        {
            activeDocumentIds.Add(sourceDocument.DocumentId);
            var updated = await UpsertFromSourceAsync(sourceDocument, configuration, kernel, forceReindex: true, cancellationToken);
            if (updated)
            {
                processed++;
            }
        }

        var staleDocuments = await _dbContext.RagVectorDocuments
            .Where(document => !activeDocumentIds.Contains(document.DocumentId))
            .ToListAsync(cancellationToken);

        var removedCount = staleDocuments.Count;
        if (removedCount > 0)
        {
            foreach (var staleDocument in staleDocuments)
            {
                if (string.IsNullOrWhiteSpace(staleDocument.EmbeddingModelId))
                {
                    continue;
                }

                await RagVectorStore.DeleteAsync(
                    _dbContext,
                    staleDocument.EmbeddingModelId,
                    staleDocument.DocumentId,
                    cancellationToken);
            }

            _dbContext.RagVectorDocuments.RemoveRange(staleDocuments);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return (processed, removedCount);
    }

    public async Task<bool> ReprocessDocumentAsync(string documentId, CancellationToken cancellationToken = default)
    {
        var normalizedId = NormalizeDocumentId(documentId);
        if (string.IsNullOrWhiteSpace(normalizedId))
        {
            return false;
        }

        await ImportLegacyKnowledgeBaseFilesAsync(cancellationToken);

        var configuration = await ResolveConfigurationAsync(cancellationToken);
        var kernel = KernelFactory.CreateKernel(configuration, out _);
        var sourceDocument = await _dbContext.RagSourceDocuments
            .AsNoTracking()
            .FirstOrDefaultAsync(document => document.DocumentId == normalizedId, cancellationToken);

        if (sourceDocument is null)
        {
            return false;
        }

        return await UpsertFromSourceAsync(sourceDocument, configuration, kernel, forceReindex: false, cancellationToken);
    }

    public async Task RemoveDocumentAsync(string documentId, CancellationToken cancellationToken = default)
    {
        var normalizedId = NormalizeDocumentId(documentId);
        if (string.IsNullOrWhiteSpace(normalizedId))
        {
            return;
        }

        var existing = await _dbContext.RagVectorDocuments
            .FirstOrDefaultAsync(document => document.DocumentId == normalizedId, cancellationToken);

        if (existing is null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(existing.EmbeddingModelId))
        {
            await RagVectorStore.DeleteAsync(_dbContext, existing.EmbeddingModelId, normalizedId, cancellationToken);
        }

        _dbContext.RagVectorDocuments.Remove(existing);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<bool> UpsertFromSourceAsync(
        RagSourceDocument sourceDocument,
        RagRuntimeConfiguration configuration,
        Kernel kernel,
        bool forceReindex,
        CancellationToken cancellationToken)
    {
        var documentId = sourceDocument.DocumentId;
        var content = sourceDocument.Content;
        if (string.IsNullOrWhiteSpace(content))
        {
            return false;
        }

        var contentHash = ComputeHash(content);
        var title = sourceDocument.Title;
        var sourceUpdatedAtUtc = sourceDocument.SourceUpdatedAtUtc;
        var sourceFileName = string.IsNullOrWhiteSpace(sourceDocument.OriginalFileName)
            ? $"{documentId}.md"
            : sourceDocument.OriginalFileName;
        var url = $"local://knowledge/{sourceFileName}";

        var existing = await _dbContext.RagVectorDocuments
            .FirstOrDefaultAsync(document => document.DocumentId == documentId, cancellationToken);

        var isMetadataCurrent = existing is not null
            && existing.ContentHash == contentHash
            && existing.SourceUpdatedAtUtc == sourceUpdatedAtUtc
            && string.Equals(existing.EmbeddingModelId, configuration.EmbeddingModelId, StringComparison.Ordinal);

        if (!forceReindex && isMetadataCurrent)
        {
            return false;
        }

        if (existing is not null
            && !string.IsNullOrWhiteSpace(existing.EmbeddingModelId)
            && !string.Equals(existing.EmbeddingModelId, configuration.EmbeddingModelId, StringComparison.Ordinal))
        {
            await RagVectorStore.DeleteAsync(_dbContext, existing.EmbeddingModelId, documentId, cancellationToken);
        }

        var embedding = await GenerateEmbeddingAsync(kernel, content, cancellationToken);

        if (embedding is not null)
        {
            var vectorCollection = RagVectorStore.CreateCollection(_dbContext, configuration.EmbeddingModelId, embedding.Length);
            await vectorCollection.EnsureCollectionExistsAsync(cancellationToken);
            await vectorCollection.UpsertAsync(new RagVectorStoreRecord
            {
                Key = documentId,
                Title = title,
                Url = url,
                Content = content,
                Embedding = embedding
            }, cancellationToken);
        }
        else if (existing is not null && !string.IsNullOrWhiteSpace(existing.EmbeddingModelId))
        {
            await RagVectorStore.DeleteAsync(_dbContext, existing.EmbeddingModelId, documentId, cancellationToken);
        }

        if (existing is null)
        {
            _dbContext.RagVectorDocuments.Add(new RagVectorDocument
            {
                DocumentId = documentId,
                Title = title,
                Url = url,
                Content = content,
                ContentHash = contentHash,
                EmbeddingJson = null,
                EmbeddingModelId = embedding is null ? null : configuration.EmbeddingModelId,
                SourceUpdatedAtUtc = sourceUpdatedAtUtc,
                IndexedAtUtc = DateTime.UtcNow
            });

            await _dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }

        if (existing.ContentHash == contentHash && existing.SourceUpdatedAtUtc == sourceUpdatedAtUtc)
        {
            return false;
        }

        existing.Title = title;
        existing.Url = url;
        existing.Content = content;
        existing.ContentHash = contentHash;
        existing.EmbeddingJson = null;
        existing.EmbeddingModelId = embedding is null ? null : configuration.EmbeddingModelId;
        existing.SourceUpdatedAtUtc = sourceUpdatedAtUtc;
        existing.IndexedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static string NormalizeDocumentId(string value)
    {
        var chars = value
            .Trim()
            .ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
            .ToArray();

        var collapsed = new string(chars);
        while (collapsed.Contains("--", StringComparison.Ordinal))
        {
            collapsed = collapsed.Replace("--", "-", StringComparison.Ordinal);
        }

        return collapsed.Trim('-');
    }

    private static string GetTitle(string content, string fallback)
    {
        foreach (var line in content.Split(["\r\n", "\n"], StringSplitOptions.None))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("# ", StringComparison.Ordinal))
            {
                return trimmed[2..].Trim();
            }
        }

        return fallback;
    }

    private async Task ImportLegacyKnowledgeBaseFilesAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_knowledgeBasePath);

        var hasChanges = false;
        foreach (var filePath in Directory.EnumerateFiles(_knowledgeBasePath, "*.md", SearchOption.TopDirectoryOnly))
        {
            var documentId = NormalizeDocumentId(Path.GetFileNameWithoutExtension(filePath));
            if (string.IsNullOrWhiteSpace(documentId))
            {
                continue;
            }

            var content = await File.ReadAllTextAsync(filePath, cancellationToken);
            if (string.IsNullOrWhiteSpace(content))
            {
                continue;
            }

            var sourceUpdatedAtUtc = File.GetLastWriteTimeUtc(filePath);
            var title = GetTitle(content, Path.GetFileNameWithoutExtension(filePath));
            var fileName = Path.GetFileName(filePath);

            var existing = await _dbContext.RagSourceDocuments
                .FirstOrDefaultAsync(document => document.DocumentId == documentId, cancellationToken);

            if (existing is null)
            {
                _dbContext.RagSourceDocuments.Add(new RagSourceDocument
                {
                    DocumentId = documentId,
                    Title = title,
                    OriginalFileName = fileName,
                    Content = content,
                    CreatedAtUtc = sourceUpdatedAtUtc,
                    SourceUpdatedAtUtc = sourceUpdatedAtUtc,
                    UpdatedAtUtc = sourceUpdatedAtUtc
                });

                hasChanges = true;
                continue;
            }

            if (existing.SourceUpdatedAtUtc >= sourceUpdatedAtUtc
                && string.Equals(existing.Content, content, StringComparison.Ordinal))
            {
                continue;
            }

            existing.Title = title;
            existing.OriginalFileName = fileName;
            existing.Content = content;
            existing.SourceUpdatedAtUtc = sourceUpdatedAtUtc;
            existing.UpdatedAtUtc = DateTime.UtcNow;
            hasChanges = true;
        }

        if (hasChanges)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private static string ComputeHash(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    private async Task<RagRuntimeConfiguration> ResolveConfigurationAsync(CancellationToken cancellationToken)
    {
        var configuration = await _dbContext.RagRuntimeConfigurations
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == 1, cancellationToken);

        if (configuration is not null)
        {
            return configuration;
        }

        return new RagRuntimeConfiguration
        {
            Id = 1,
            OpenAIBaseUrl = _ragOptions.OpenAIBaseUrl,
            ModelId = _ragOptions.ModelId,
            EmbeddingModelId = _ragOptions.EmbeddingModelId,
            OpenAIApiKey = _ragOptions.OpenAIApiKey,
            TopK = _ragOptions.TopK,
            UpdatedAtUtc = DateTime.UtcNow
        };
    }

    private async Task<float[]?> GenerateEmbeddingAsync(Kernel kernel, string text, CancellationToken cancellationToken)
    {
        try
        {
            var embeddingGenerator = kernel.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();
            var embedding = await embeddingGenerator.GenerateAsync(text, cancellationToken: cancellationToken);
            return embedding.Vector.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate embedding through Semantic Kernel while indexing document.");
            return null;
        }
    }
}

#pragma warning restore SKEXP0001
