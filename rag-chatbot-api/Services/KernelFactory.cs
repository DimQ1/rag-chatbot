using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using rag_chatbot_api.Models;

#pragma warning disable SKEXP0010

namespace rag_chatbot_api.Services;

public static class KernelFactory
{
    public static Kernel CreateKernel(RagRuntimeConfiguration configuration, out KernelFactorySettings settings)
    {
        settings = KernelFactorySettings.FromConfiguration(configuration);

        var builder = Kernel.CreateBuilder();

        if (!settings.HasRequiredConfiguration)
        {
            return builder.Build();
        }

        builder.AddOpenAIChatCompletion(
            modelId: settings.ChatModelId,
            apiKey: settings.ApiKey,
            httpClient: CreateHttpClient(settings.BaseUrl));

        builder.Services.AddOpenAIEmbeddingGenerator(
            modelId: settings.EmbeddingModelId,
            apiKey: settings.ApiKey,
            httpClient: CreateHttpClient(settings.BaseUrl));

        return builder.Build();
    }

    private static HttpClient? CreateHttpClient(string? baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return null;
        }

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var endpoint))
        {
            return null;
        }

        return new HttpClient
        {
            BaseAddress = endpoint
        };
    }
}

#pragma warning restore SKEXP0010

public sealed record KernelFactorySettings(
    string? BaseUrl,
    string ChatModelId,
    string EmbeddingModelId,
    string ApiKey)
{
    public bool HasRequiredConfiguration =>
        !string.IsNullOrWhiteSpace(ApiKey) &&
        !string.IsNullOrWhiteSpace(ChatModelId) &&
        !string.IsNullOrWhiteSpace(EmbeddingModelId);

    public static KernelFactorySettings FromConfiguration(RagRuntimeConfiguration configuration)
    {
        return new KernelFactorySettings(
            BaseUrl: configuration.OpenAIBaseUrl,
            ChatModelId: configuration.ModelId,
            EmbeddingModelId: configuration.EmbeddingModelId,
            ApiKey: configuration.OpenAIApiKey);
    }
}
