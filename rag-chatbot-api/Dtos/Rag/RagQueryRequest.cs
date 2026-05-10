using System.ComponentModel.DataAnnotations;

namespace rag_chatbot_api.Dtos.Rag;

public class RagQueryRequest
{
    [Required]
    [MinLength(2)]
    public string Question { get; set; } = string.Empty;

    public bool IncludeReasoning { get; set; }
}
