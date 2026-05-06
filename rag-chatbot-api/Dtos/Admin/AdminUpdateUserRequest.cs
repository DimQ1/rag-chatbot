using System.ComponentModel.DataAnnotations;

namespace rag_chatbot_api.Dtos.Admin;

public class AdminUpdateUserRequest
{
    [Required]
    [MinLength(2)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [RegularExpression("^(User|Admin)$")]
    public string Role { get; set; } = "User";

    [MinLength(6)]
    public string? NewPassword { get; set; }
}
