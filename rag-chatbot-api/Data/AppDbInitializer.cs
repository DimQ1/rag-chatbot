using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using rag_chatbot_api.Models;
using rag_chatbot_api.Options;
using rag_chatbot_api.Services;

namespace rag_chatbot_api.Data;

/// <summary>
/// Initializes the application database with schema and seed data.
/// Delegates to specialized seeders for each concern (admin user, RAG config).
/// Note: Schema should be managed through EF Core migrations in production.
/// </summary>
public static class AppDbInitializer
{
    /// <summary>
    /// Initializes the database schema and seeds initial data.
    /// Resolves dependencies from the service provider using scoped lifetime.
    /// </summary>
    public static async Task InitializeAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var serviceProvider = scope.ServiceProvider;
        var dbContext = serviceProvider.GetRequiredService<AppDbContext>();

        // Ensure schema is created (EF Core model-based)
        await dbContext.Database.EnsureCreatedAsync();

        // Ensure additive schema updates for existing SQLite databases.
        await EnsureAgentSessionStateSchemaAsync(dbContext);

        // Seed initial data
        var adminOptions = serviceProvider.GetRequiredService<IOptions<AdminOptions>>().Value;
        var ragOptions = serviceProvider.GetRequiredService<IOptions<RagOptions>>().Value;

        await SeedRagConfigurationAsync(dbContext, ragOptions);
        await SeedAdminUserAsync(dbContext, adminOptions);
    }

    private static Task EnsureAgentSessionStateSchemaAsync(AppDbContext dbContext)
    {
        const string sql =
            """
            CREATE TABLE IF NOT EXISTS "AgentSessionStates" (
                "ChatSessionId" TEXT NOT NULL CONSTRAINT "PK_AgentSessionStates" PRIMARY KEY,
                "SerializedSession" TEXT NOT NULL,
                "UpdatedAtUtc" TEXT NOT NULL
            );
            """;

        return dbContext.Database.ExecuteSqlRawAsync(sql);
    }

    /// <summary>
    /// Seeds the RAG runtime configuration if it doesn't exist.
    /// Uses EF Core change tracking instead of raw SQL.
    /// </summary>
    private static async Task SeedRagConfigurationAsync(AppDbContext dbContext, RagOptions ragOptions)
    {
        // Check if configuration already exists
        var existingConfig = await dbContext.RagRuntimeConfigurations
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == 1);

        if (existingConfig is not null)
        {
            // Update embedding model if not set
            if (!string.IsNullOrWhiteSpace(existingConfig.EmbeddingModelId))
            {
                return;
            }

            existingConfig.EmbeddingModelId = ragOptions.EmbeddingModelId;
            existingConfig.UpdatedAtUtc = DateTime.UtcNow;
            dbContext.RagRuntimeConfigurations.Update(existingConfig);
            await dbContext.SaveChangesAsync();
            return;
        }

        // Create new configuration
        var newConfig = new RagRuntimeConfiguration
        {
            Id = 1,
            OpenAIBaseUrl = ragOptions.OpenAIBaseUrl,
            ModelId = ragOptions.ModelId,
            EmbeddingModelId = ragOptions.EmbeddingModelId,
            OpenAIApiKey = ragOptions.OpenAIApiKey,
            TopK = ragOptions.TopK,
            UpdatedAtUtc = DateTime.UtcNow
        };

        dbContext.RagRuntimeConfigurations.Add(newConfig);
        await dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// Seeds the admin user account based on configuration options.
    /// Creates or updates an existing admin user with current credentials.
    /// </summary>
    private static async Task SeedAdminUserAsync(AppDbContext dbContext, AdminOptions adminOptions)
    {
        if (!adminOptions.SeedAccount)
        {
            return;
        }

        var normalizedEmail = NormalizeEmail(adminOptions.Email);
        if (string.IsNullOrWhiteSpace(normalizedEmail) || string.IsNullOrWhiteSpace(adminOptions.Password))
        {
            return;
        }

        var existingUser = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail);

        var normalizedName = string.IsNullOrWhiteSpace(adminOptions.Name)
            ? "Administrator"
            : adminOptions.Name.Trim();

        var (passwordHash, passwordSalt) = PasswordService.HashPassword(adminOptions.Password);

        if (existingUser is null)
        {
            // Create new admin user
            var newUser = new AppUser
            {
                Email = normalizedEmail,
                Name = normalizedName,
                PasswordHash = passwordHash,
                PasswordSalt = passwordSalt,
                Role = "Admin"
            };

            dbContext.Users.Add(newUser);
        }
        else
        {
            // Update existing user
            existingUser.Role = "Admin";
            existingUser.Name = normalizedName;

            // Reset password if explicitly requested or if not set
            if (adminOptions.ResetPasswordOnStartup ||
                string.IsNullOrWhiteSpace(existingUser.PasswordHash) ||
                string.IsNullOrWhiteSpace(existingUser.PasswordSalt))
            {
                existingUser.PasswordHash = passwordHash;
                existingUser.PasswordSalt = passwordSalt;
            }

            dbContext.Users.Update(existingUser);
        }

        await dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// Normalizes an email address for consistent lookups.
    /// </summary>
    private static string NormalizeEmail(string? email)
    {
        return string.IsNullOrWhiteSpace(email)
            ? string.Empty
            : email.Trim().ToLowerInvariant();
    }
}
