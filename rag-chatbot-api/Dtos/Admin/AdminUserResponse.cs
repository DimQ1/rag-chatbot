namespace rag_chatbot_api.Dtos.Admin;

public class AdminUserResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool HasPassword { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
