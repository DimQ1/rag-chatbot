namespace rag_chatbot_api.Options;

public class AdminOptions
{
    public const string SectionName = "Admin";

    public bool SeedAccount { get; set; }
    public bool ResetPasswordOnStartup { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = "Administrator";
    public string Password { get; set; } = string.Empty;
}
