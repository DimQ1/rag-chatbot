using System.ComponentModel.DataAnnotations;

namespace rag_chatbot_api.Dtos.Admin;

public class AdminUpdateRagConfigurationRequest
{
    [Required]
    [MinLength(10)]
    public string OpenAIBaseUrl { get; set; } = string.Empty;

    [Required]
    [MinLength(2)]
    public string ModelId { get; set; } = string.Empty;

    [Required]
    [MinLength(2)]
    public string EmbeddingModelId { get; set; } = string.Empty;

    public string OpenAIApiKey { get; set; } = string.Empty;

    [Range(1, 10)]
    public int TopK { get; set; } = 3;
}
