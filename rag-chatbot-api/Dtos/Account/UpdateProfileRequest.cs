using System.ComponentModel.DataAnnotations;

namespace rag_chatbot_api.Dtos.Account;

public class UpdateProfileRequest
{
    [Required]
    [MinLength(2)]
    public string Name { get; set; } = string.Empty;
}
