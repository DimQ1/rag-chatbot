using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using rag_chatbot_api.Models;
using rag_chatbot_api.Options;
using rag_chatbot_api.Services;

namespace rag_chatbot_api.Data;

public static class AppDbInitializer
{
    private const string AdminRole = "Admin";
    private const string DefaultAdminName = "Administrator";

    public static async Task InitializeAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var serviceProvider = scope.ServiceProvider;
        var dbContext = serviceProvider.GetRequiredService<AppDbContext>();
        var adminOptions = serviceProvider.GetRequiredService<IOptions<AdminOptions>>().Value;
        var ragOptions = serviceProvider.GetRequiredService<IOptions<RagOptions>>().Value;

        EnsureDatabaseSchema(dbContext);
        await EnsureRagConfigurationAsync(dbContext, ragOptions);
        await EnsureAdminUserAsync(dbContext, adminOptions);
    }

    private static void EnsureDatabaseSchema(AppDbContext dbContext)
    {
        dbContext.Database.EnsureCreated();
        EnsureRagRuntimeConfigurationTable(dbContext);
        EnsureEmbeddingModelColumn(dbContext);
        EnsureSourceDocumentTable(dbContext);
        EnsureVectorDocumentTable(dbContext);
        EnsureChatSessionTables(dbContext);
        EnsureChatSessionIndexes(dbContext);
    }

    private static void EnsureRagRuntimeConfigurationTable(AppDbContext dbContext)
    {
        dbContext.Database.ExecuteSqlRaw(
            """
            CREATE TABLE IF NOT EXISTS RagRuntimeConfigurations (
                Id INTEGER NOT NULL CONSTRAINT PK_RagRuntimeConfigurations PRIMARY KEY,
                OpenAIBaseUrl TEXT NOT NULL,
                ModelId TEXT NOT NULL,
                EmbeddingModelId TEXT NOT NULL DEFAULT 'text-embedding-3-small',
                OpenAIApiKey TEXT NOT NULL,
                TopK INTEGER NOT NULL,
                UpdatedAtUtc TEXT NOT NULL
            );
            """);
    }

    private static void EnsureChatSessionTables(AppDbContext dbContext)
    {
        dbContext.Database.ExecuteSqlRaw(
            """
            CREATE TABLE IF NOT EXISTS ChatSessions (
                Id TEXT NOT NULL PRIMARY KEY,
                UserId TEXT NOT NULL,
                Topic TEXT NOT NULL,
                IsCustomTopic INTEGER NOT NULL DEFAULT 0,
                IsPinned INTEGER NOT NULL DEFAULT 0,
                CreatedAtUtc TEXT NOT NULL,
                UpdatedAtUtc TEXT NOT NULL,
                DeletedAtUtc TEXT NULL,
                FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
            );
            """);

        dbContext.Database.ExecuteSqlRaw(
            """
            CREATE TABLE IF NOT EXISTS ChatSessionMessages (
                Id TEXT NOT NULL PRIMARY KEY,
                SessionId TEXT NOT NULL,
                Role TEXT NOT NULL,
                Content TEXT NOT NULL,
                Sources TEXT NULL,
                CreatedAtUtc TEXT NOT NULL,
                MessageOrder INTEGER NOT NULL,
                FOREIGN KEY (SessionId) REFERENCES ChatSessions(Id) ON DELETE CASCADE
            );
            """);
    }

    private static void EnsureChatSessionIndexes(AppDbContext dbContext)
    {
        dbContext.Database.ExecuteSqlRaw(
            """
            CREATE INDEX IF NOT EXISTS IX_ChatSessions_UserId_DeletedAtUtc
            ON ChatSessions(UserId, DeletedAtUtc);
            """);

        dbContext.Database.ExecuteSqlRaw(
            """
            CREATE INDEX IF NOT EXISTS IX_ChatSessionMessages_SessionId_MessageOrder
            ON ChatSessionMessages(SessionId, MessageOrder);
            """);
    }

    private static async Task EnsureAdminUserAsync(AppDbContext dbContext, AdminOptions adminOptions)
    {
        if (!adminOptions.SeedAccount)
        {
            return;
        }

        var normalizedEmail = adminOptions.Email.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalizedEmail) || string.IsNullOrWhiteSpace(adminOptions.Password))
        {
            return;
        }

        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail);
        var (hash, salt) = PasswordService.HashPassword(adminOptions.Password);
        var normalizedName = string.IsNullOrWhiteSpace(adminOptions.Name)
            ? DefaultAdminName
            : adminOptions.Name.Trim();

        if (user is null)
        {
            dbContext.Users.Add(new AppUser
            {
                Email = normalizedEmail,
                Name = normalizedName,
                PasswordHash = hash,
                PasswordSalt = salt,
                Role = AdminRole
            });

            await dbContext.SaveChangesAsync();
            return;
        }

        user.Role = AdminRole;
        user.Name = normalizedName;

        if (adminOptions.ResetPasswordOnStartup || string.IsNullOrWhiteSpace(user.PasswordHash) || string.IsNullOrWhiteSpace(user.PasswordSalt))
        {
            user.PasswordHash = hash;
            user.PasswordSalt = salt;
        }

        await dbContext.SaveChangesAsync();
    }

    private static async Task EnsureRagConfigurationAsync(AppDbContext dbContext, RagOptions ragOptions)
    {
        var config = await dbContext.RagRuntimeConfigurations.FirstOrDefaultAsync(c => c.Id == 1);
        if (config is not null)
        {
            if (string.IsNullOrWhiteSpace(config.EmbeddingModelId))
            {
                config.EmbeddingModelId = ragOptions.EmbeddingModelId;
                config.UpdatedAtUtc = DateTime.UtcNow;
                await dbContext.SaveChangesAsync();
            }

            return;
        }

        dbContext.RagRuntimeConfigurations.Add(new RagRuntimeConfiguration
        {
            Id = 1,
            OpenAIBaseUrl = ragOptions.OpenAIBaseUrl,
            ModelId = ragOptions.ModelId,
            EmbeddingModelId = ragOptions.EmbeddingModelId,
            OpenAIApiKey = ragOptions.OpenAIApiKey,
            TopK = ragOptions.TopK,
            UpdatedAtUtc = DateTime.UtcNow
        });

        await dbContext.SaveChangesAsync();
    }

    private static void EnsureEmbeddingModelColumn(AppDbContext dbContext)
    {
        AddColumnIfMissing(
            dbContext,
            "RagRuntimeConfigurations",
            "EmbeddingModelId",
            "TEXT NOT NULL DEFAULT 'text-embedding-3-small'");
    }

    private static void EnsureVectorDocumentTable(AppDbContext dbContext)
    {
        dbContext.Database.ExecuteSqlRaw(
            """
            CREATE TABLE IF NOT EXISTS RagVectorDocuments (
                Id INTEGER NOT NULL CONSTRAINT PK_RagVectorDocuments PRIMARY KEY AUTOINCREMENT,
                DocumentId TEXT NOT NULL,
                Title TEXT NOT NULL,
                Url TEXT NOT NULL,
                Content TEXT NOT NULL,
                ContentHash TEXT NOT NULL,
                EmbeddingJson TEXT NULL,
                EmbeddingModelId TEXT NULL,
                SourceUpdatedAtUtc TEXT NOT NULL,
                IndexedAtUtc TEXT NOT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS IX_RagVectorDocuments_DocumentId ON RagVectorDocuments (DocumentId);
            """);

        AddColumnIfMissing(dbContext, "RagVectorDocuments", "EmbeddingJson", "TEXT NULL");
        AddColumnIfMissing(dbContext, "RagVectorDocuments", "EmbeddingModelId", "TEXT NULL");
    }

    private static void AddColumnIfMissing(AppDbContext dbContext, string tableName, string columnName, string columnDefinition)
    {
        if (ColumnExists(dbContext, tableName, columnName))
        {
            return;
        }

        ExecuteWithOpenConnectionVoid(dbContext, connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = $"ALTER TABLE \"{tableName}\" ADD COLUMN \"{columnName}\" {columnDefinition};";
            command.ExecuteNonQuery();
        });
    }

    private static bool ColumnExists(AppDbContext dbContext, string tableName, string columnName)
    {
        return ExecuteWithOpenConnection(dbContext, connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = $"PRAGMA table_info(\"{tableName}\");";
            using var reader = command.ExecuteReader();

            while (reader.Read())
            {
                var currentColumnName = reader["name"]?.ToString();
                if (string.Equals(currentColumnName, columnName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        });
    }

    private static T ExecuteWithOpenConnection<T>(AppDbContext dbContext, Func<System.Data.Common.DbConnection, T> action)
    {
        var connection = dbContext.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;

        if (shouldClose)
        {
            connection.Open();
        }

        try
        {
            return action(connection);
        }
        finally
        {
            if (shouldClose)
            {
                connection.Close();
            }
        }
    }

    private static void ExecuteWithOpenConnectionVoid(AppDbContext dbContext, Action<System.Data.Common.DbConnection> action)
    {
        ExecuteWithOpenConnection(dbContext, connection =>
        {
            action(connection);
            return true;
        });
    }

    private static void EnsureSourceDocumentTable(AppDbContext dbContext)
    {
        dbContext.Database.ExecuteSqlRaw(
            """
            CREATE TABLE IF NOT EXISTS RagSourceDocuments (
                Id INTEGER NOT NULL CONSTRAINT PK_RagSourceDocuments PRIMARY KEY AUTOINCREMENT,
                DocumentId TEXT NOT NULL,
                Title TEXT NOT NULL,
                OriginalFileName TEXT NOT NULL,
                Content TEXT NOT NULL,
                CreatedAtUtc TEXT NOT NULL,
                SourceUpdatedAtUtc TEXT NOT NULL,
                UpdatedAtUtc TEXT NOT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS IX_RagSourceDocuments_DocumentId ON RagSourceDocuments (DocumentId);
            """);
    }
}
