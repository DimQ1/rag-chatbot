using System.ComponentModel.DataAnnotations;

namespace rag_chatbot_api.Dtos.Auth;

public class GoogleLoginRequest
{
    [Required]
    public string IdToken { get; set; } = string.Empty;
}
