using System.ComponentModel.DataAnnotations;

namespace rag_chatbot_api.Dtos.Admin;

public class AdminUpsertDocumentRequest
{
    [Required]
    [MinLength(2)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [MinLength(10)]
    public string Content { get; set; } = string.Empty;
}
