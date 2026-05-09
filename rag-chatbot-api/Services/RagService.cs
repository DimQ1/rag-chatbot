using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using rag_chatbot_api.Data;
using rag_chatbot_api.Dtos.Rag;
using rag_chatbot_api.Models;
using rag_chatbot_api.Options;

#pragma warning disable SKEXP0001

namespace rag_chatbot_api.Services;

public class RagService(
    AppDbContext dbContext,
    IOptions<RagOptions> ragOptions,
    ILogger<RagService> logger) : IRagService
{
    private readonly AppDbContext _dbContext = dbContext;
    private readonly RagOptions _ragOptions = ragOptions.Value;
    private readonly ILogger<RagService> _logger = logger;

    public async Task<RagQueryResponse> QueryAsync(string question, CancellationToken cancellationToken = default)
    {
        var configuration = await ResolveConfigurationAsync(cancellationToken);
        var kernel = KernelFactory.CreateKernel(configuration, out var settings);

        if (!settings.HasRequiredConfiguration)
        {
            return new RagQueryResponse
            {
                Answer = "RAG is not fully configured. Set the API key, chat model, and embedding model in admin configuration.",
                Sources = []
            };
        }

        var hasIndexedDocuments = await _dbContext.RagVectorDocuments
            .AsNoTracking()
            .AnyAsync(
                document => document.EmbeddingModelId == configuration.EmbeddingModelId,
                cancellationToken);

        var hasOutdatedEmbeddings = await _dbContext.RagVectorDocuments
            .AsNoTracking()
            .AnyAsync(
                document => !string.IsNullOrWhiteSpace(document.EmbeddingModelId)
                    && document.EmbeddingModelId != configuration.EmbeddingModelId,
                cancellationToken);

        if (hasOutdatedEmbeddings)
        {
            return new RagQueryResponse
            {
                Answer = "Indexed documents were generated with a different embedding model. Reprocess the knowledge base from the admin panel.",
                Sources = []
            };
        }

        if (!hasIndexedDocuments)
        {
            return new RagQueryResponse
            {
                Answer = "No indexed RAG documents are available. Reprocess the knowledge base from the admin panel.",
                Sources = []
            };
        }

        var queryEmbedding = await TryGenerateEmbeddingAsync(kernel, question, cancellationToken);
        if (queryEmbedding is null)
        {
            return new RagQueryResponse
            {
                Answer = "The embedding request failed. Verify the AI endpoint and embedding model configuration.",
                Sources = []
            };
        }

        var topK = Math.Clamp(configuration.TopK, 1, 10);
        var retrieved = await RetrieveRelevantAsync(
            queryEmbedding,
            configuration.EmbeddingModelId,
            topK,
            cancellationToken);

        if (retrieved is null)
        {
            return new RagQueryResponse
            {
                Answer = "The vector search request failed. Verify the SQLite vector extension is available and reprocess the knowledge base.",
                Sources = []
            };
        }

        if (retrieved.Count == 0)
        {
            return new RagQueryResponse
            {
                Answer = "I could not find any relevant knowledge documents for your question.",
                Sources = []
            };
        }

        var answer = await GenerateAnswerAsync(kernel, question, retrieved, cancellationToken);

        return new RagQueryResponse
        {
            Answer = answer,
            Sources = retrieved.Select(d => new RagSource
            {
                Title = d.Title,
                Url = $"/document/{Uri.EscapeDataString(d.DocumentId)}"
            })
        };
    }

    private async Task<List<KnowledgeDocument>?> RetrieveRelevantAsync(
        float[] queryEmbedding,
        string embeddingModelId,
        int topK,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!await RagVectorStore.CollectionExistsAsync(_dbContext, embeddingModelId, cancellationToken))
            {
                return [];
            }

            var collection = RagVectorStore.CreateCollection(_dbContext, embeddingModelId, queryEmbedding.Length);
            var documents = new List<KnowledgeDocument>();

            await foreach (var result in collection.SearchAsync(
                queryEmbedding,
                topK,
                cancellationToken: cancellationToken))
            {
                documents.Add(new KnowledgeDocument(
                    result.Record.Key,
                    result.Record.Title,
                    result.Record.Url,
                    result.Record.Content));
            }

            return documents;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Vector search failed through SqliteVec.");
            return null;
        }
    }

    private async Task<string> GenerateAnswerAsync(
        Kernel kernel,
        string question,
        IReadOnlyCollection<KnowledgeDocument> documents,
        CancellationToken cancellationToken)
    {
        var context = string.Join("\n\n", documents.Select(d =>
            $"Source: {d.Title} ({d.Url})\n{d.Content}"));

        try
        {
            var prompt =
                """
                You are a helpful assistant. Use only the provided context.
                If the answer is not present in the context, say you do not know.
                You must return a concise answer based on the provided information, and translate it to the language of the question if necessary. 
                Don't return any markup, only plain text. 

                Context:
                {{$context}}

                Question: {{$question}}

                Return a concise plain-text answer.
                """;

            var result = await kernel.InvokePromptAsync(
                prompt,
                new KernelArguments
                {
                    ["context"] = context,
                    ["question"] = question
                },
                cancellationToken: cancellationToken);

            var content = result.ToString();
            if (string.IsNullOrWhiteSpace(content))
            {
                return "The AI model returned an empty response.";
            }

            return content.Trim();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI completion request failed.");
            return "The AI completion request failed. Verify the AI endpoint and chat model configuration.";
        }
    }

    private async Task<float[]?> TryGenerateEmbeddingAsync(
        Kernel kernel,
        string text,
        CancellationToken cancellationToken)
    {
        try
        {
            var embeddingGenerator = kernel.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();
            var embedding = await embeddingGenerator.GenerateAsync(text, cancellationToken: cancellationToken);
            return embedding.Vector.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Embedding request failed through Semantic Kernel.");
            return null;
        }
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

    private sealed record KnowledgeDocument(string DocumentId, string Title, string Url, string Content);
}

#pragma warning restore SKEXP0001
