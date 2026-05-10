using System.ClientModel;
using System.Text;
using Microsoft.Agents.AI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Embeddings;
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

    public async Task<RagQueryResponse> QueryAsync(string question, Guid? chatSessionId = null, bool includeReasoning = false, CancellationToken cancellationToken = default)
    {
        var configuration = await ResolveConfigurationAsync(cancellationToken);
        if (!HasRequiredConfiguration(configuration))
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

        var queryEmbedding = await TryGenerateEmbeddingAsync(configuration, question, cancellationToken);
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

        var answer = await GenerateAnswerAsync(configuration, question, retrieved, chatSessionId, includeReasoning, cancellationToken);

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
        RagRuntimeConfiguration configuration,
        string question,
        IReadOnlyCollection<KnowledgeDocument> documents,
        Guid? chatSessionId,
        bool includeReasoning,
        CancellationToken cancellationToken)
    {
        var context = string.Join("\n\n", documents.Select(d =>
            $"Source: {d.Title} ({d.Url})\n{d.Content}"));

        try
        {
            var endpoint = TryResolveEndpoint(configuration.OpenAIBaseUrl);
            var clientOptions = endpoint is null
                ? new OpenAIClientOptions()
                : new OpenAIClientOptions { Endpoint = endpoint };

            var responseStyleInstructions = includeReasoning
                ? "Provide the final answer first. You may include a short, user-facing rationale when helpful. Do not reveal hidden chain-of-thought or internal notes."
                : "Never output reasoning steps, analysis notes, or chain-of-thought. Return only the final user-facing answer.";

            var agentInstructions =
                "You are a helpful assistant. Use only the provided context. " +
                "When the question includes a session-memory block, you may use that memory to answer follow-up questions. " +
                "If the answer is not present in the context, say you do not know. " +
                "Translate your response to the language of the question if necessary. " +
                "Format your response as Markdown. " +
                responseStyleInstructions + " " +
                "Respond naturally and directly, and avoid rigid templates unless the user explicitly asks for a specific format.";

            var agent = new OpenAIClient(
                    new ApiKeyCredential(configuration.OpenAIApiKey),
                    clientOptions)
                .GetChatClient(configuration.ModelId)
                .AsAIAgent(
                    new ChatClientAgentOptions
                    {
                        Name = "Help Assistant",
                        ChatOptions = new ChatOptions
                        {
                            Instructions = agentInstructions
                        },
                        ChatHistoryProvider = chatSessionId is Guid providerSessionId
                            ? new DbChatHistoryProvider(_dbContext, providerSessionId)
                            : null
                    });

            var prompt =
                $"""
                Context:
                {context}

                Question: {question}
                """;

            var session = await agent.CreateSessionAsync(cancellationToken);

            var responseBuilder = new StringBuilder();
            await foreach (var update in agent.RunStreamingAsync(
                prompt,
                session,
                cancellationToken: cancellationToken))
            {
                var hadTextInUpdate = false;
                foreach (var item in update.Contents)
                {
                    var extracted = TryExtractText(item);
                    if (!string.IsNullOrWhiteSpace(extracted))
                    {
                        responseBuilder.Append(extracted);
                        hadTextInUpdate = true;
                    }
                }

                // Some providers surface text at the update-level rather than as TextContent items.
                if (!hadTextInUpdate)
                {
                    var updateText = update.ToString();
                    if (!string.IsNullOrWhiteSpace(updateText) && !string.Equals(updateText, update.GetType().ToString(), StringComparison.Ordinal))
                    {
                        responseBuilder.Append(updateText);
                    }
                }
            }

            var content = responseBuilder.ToString();
            if (string.IsNullOrWhiteSpace(content))
            {
                return "The AI model returned an empty response.";
            }

            return SanitizeAssistantResponse(content);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI completion request failed.");
            return "The AI completion request failed. Verify the AI endpoint and chat model configuration.";
        }
    }

    private static Uri? TryResolveEndpoint(string? baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return null;
        }

        return Uri.TryCreate(baseUrl, UriKind.Absolute, out var endpoint)
            ? endpoint
            : null;
    }

    private async Task<float[]?> TryGenerateEmbeddingAsync(
        RagRuntimeConfiguration configuration,
        string text,
        CancellationToken cancellationToken)
    {
        try
        {
            var endpoint = TryResolveEndpoint(configuration.OpenAIBaseUrl);
            var clientOptions = endpoint is null
                ? new OpenAIClientOptions()
                : new OpenAIClientOptions { Endpoint = endpoint };

            var embeddingClient = new OpenAIClient(
                    new ApiKeyCredential(configuration.OpenAIApiKey),
                    clientOptions)
                .GetEmbeddingClient(configuration.EmbeddingModelId);

            var embeddingResponse = await embeddingClient.GenerateEmbeddingAsync(
                text,
                cancellationToken: cancellationToken);

            OpenAIEmbedding embedding = embeddingResponse.Value;
            return embedding.ToFloats().ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Embedding request failed through OpenAI embedding client.");
            return null;
        }
    }

    private static bool HasRequiredConfiguration(RagRuntimeConfiguration configuration)
    {
        return !string.IsNullOrWhiteSpace(configuration.OpenAIApiKey)
            && !string.IsNullOrWhiteSpace(configuration.ModelId)
            && !string.IsNullOrWhiteSpace(configuration.EmbeddingModelId);
    }

    private static string? TryExtractText(object contentItem)
    {
        if (contentItem is Microsoft.Extensions.AI.TextContent aiText)
        {
            return aiText.Text;
        }

        var type = contentItem.GetType();
        var textProperty = type.GetProperty("Text");
        if (textProperty is not null && textProperty.PropertyType == typeof(string))
        {
            return textProperty.GetValue(contentItem) as string;
        }

        var fallback = contentItem.ToString();
        if (string.IsNullOrWhiteSpace(fallback))
        {
            return null;
        }

        return string.Equals(fallback, type.ToString(), StringComparison.Ordinal)
            ? null
            : fallback;
    }

    private static string SanitizeAssistantResponse(string content)
    {
        var trimmed = content.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return "The AI model returned an empty response.";
        }

        // If a model leaks internal scaffolding (e.g. "Thinking Process"), keep only the final user-facing line.
        if (trimmed.Contains("Thinking Process", StringComparison.OrdinalIgnoreCase))
        {
            var lines = trimmed
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToList();

            for (var index = lines.Count - 1; index >= 0; index--)
            {
                var line = lines[index];
                if (line.StartsWith("Thinking Process", StringComparison.OrdinalIgnoreCase)
                    || line.StartsWith("Final Output", StringComparison.OrdinalIgnoreCase)
                    || line.StartsWith("Final Answer", StringComparison.OrdinalIgnoreCase)
                    || char.IsDigit(line[0]) && line.Length > 1 && (line[1] == '.' || line[1] == ')'))
                {
                    continue;
                }

                return line;
            }
        }

        return trimmed;
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
