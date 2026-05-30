using Microsoft.EntityFrameworkCore;
using rag_chatbot_api.Models;

namespace rag_chatbot_api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<ApplicationLogEntry> ApplicationLogEntries => Set<ApplicationLogEntry>();
    public DbSet<AgentSessionState> AgentSessionStates => Set<AgentSessionState>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<RagRuntimeConfiguration> RagRuntimeConfigurations => Set<RagRuntimeConfiguration>();
    public DbSet<RagSourceDocument> RagSourceDocuments => Set<RagSourceDocument>();
    public DbSet<RagVectorDocument> RagVectorDocuments => Set<RagVectorDocument>();
    public DbSet<ChatSession> ChatSessions => Set<ChatSession>();
    public DbSet<ChatSessionMessage> ChatSessionMessages => Set<ChatSessionMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ApplicationLogEntry>()
            .HasKey(logEntry => logEntry.Id);

        modelBuilder.Entity<ApplicationLogEntry>()
            .HasIndex(logEntry => logEntry.TimestampUtc);

        modelBuilder.Entity<ApplicationLogEntry>()
            .HasIndex(logEntry => new { logEntry.Level, logEntry.TimestampUtc });

        modelBuilder.Entity<ApplicationLogEntry>()
            .Property(logEntry => logEntry.Level)
            .HasMaxLength(32)
            .IsRequired();

        modelBuilder.Entity<ApplicationLogEntry>()
            .Property(logEntry => logEntry.Category)
            .HasMaxLength(256)
            .IsRequired();

        modelBuilder.Entity<ApplicationLogEntry>()
            .Property(logEntry => logEntry.Message)
            .HasMaxLength(4000)
            .IsRequired();

        modelBuilder.Entity<ApplicationLogEntry>()
            .Property(logEntry => logEntry.Exception)
            .HasMaxLength(16000);

        modelBuilder.Entity<ApplicationLogEntry>()
            .Property(logEntry => logEntry.EventName)
            .HasMaxLength(128);

        modelBuilder.Entity<ApplicationLogEntry>()
            .Property(logEntry => logEntry.TraceId)
            .HasMaxLength(128);

        modelBuilder.Entity<ApplicationLogEntry>()
            .Property(logEntry => logEntry.RequestPath)
            .HasMaxLength(256);

        modelBuilder.Entity<ApplicationLogEntry>()
            .Property(logEntry => logEntry.RequestMethod)
            .HasMaxLength(16);

        modelBuilder.Entity<ApplicationLogEntry>()
            .Property(logEntry => logEntry.UserId)
            .HasMaxLength(64);

        modelBuilder.Entity<AppUser>()
            .HasIndex(u => u.Email)
            .IsUnique();

        modelBuilder.Entity<AppUser>()
            .Property(u => u.Role)
            .HasMaxLength(32)
            .HasDefaultValue("User");

        modelBuilder.Entity<RagRuntimeConfiguration>()
            .HasKey(c => c.Id);

        modelBuilder.Entity<RagRuntimeConfiguration>()
            .Property(c => c.OpenAIBaseUrl)
            .HasMaxLength(512);

        modelBuilder.Entity<RagRuntimeConfiguration>()
            .Property(c => c.ModelId)
            .HasMaxLength(128);

        modelBuilder.Entity<RagRuntimeConfiguration>()
            .Property(c => c.EmbeddingModelId)
            .HasMaxLength(128);

        modelBuilder.Entity<AgentSessionState>()
            .HasKey(s => s.ChatSessionId);

        modelBuilder.Entity<AgentSessionState>()
            .Property(s => s.SerializedSession)
            .IsRequired();

        modelBuilder.Entity<RagSourceDocument>()
            .HasIndex(d => d.DocumentId)
            .IsUnique();

        modelBuilder.Entity<RagSourceDocument>()
            .Property(d => d.DocumentId)
            .HasMaxLength(200);

        modelBuilder.Entity<RagSourceDocument>()
            .Property(d => d.Title)
            .HasMaxLength(400);

        modelBuilder.Entity<RagSourceDocument>()
            .Property(d => d.OriginalFileName)
            .HasMaxLength(260);

        modelBuilder.Entity<RagVectorDocument>()
            .HasIndex(d => d.DocumentId)
            .IsUnique();

        modelBuilder.Entity<RagVectorDocument>()
            .Property(d => d.DocumentId)
            .HasMaxLength(200);

        modelBuilder.Entity<RagVectorDocument>()
            .Property(d => d.Title)
            .HasMaxLength(400);

        modelBuilder.Entity<RagVectorDocument>()
            .Property(d => d.Url)
            .HasMaxLength(512);

        modelBuilder.Entity<RagVectorDocument>()
            .Property(d => d.ContentHash)
            .HasMaxLength(128);

        modelBuilder.Entity<RagVectorDocument>()
            .Property(d => d.EmbeddingModelId)
            .HasMaxLength(128);

        // ChatSession configurations
        modelBuilder.Entity<ChatSession>()
            .HasKey(s => s.Id);

        modelBuilder.Entity<ChatSession>()
            .HasIndex(s => new { s.UserId, s.DeletedAtUtc });

        modelBuilder.Entity<ChatSession>()
            .Property(s => s.Topic)
            .HasMaxLength(200)
            .IsRequired();

        modelBuilder.Entity<ChatSession>()
            .HasOne(s => s.User)
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ChatSession>()
            .HasMany(s => s.Messages)
            .WithOne(m => m.Session)
            .HasForeignKey(m => m.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        // ChatSessionMessage configurations
        modelBuilder.Entity<ChatSessionMessage>()
            .HasKey(m => m.Id);

        modelBuilder.Entity<ChatSessionMessage>()
            .HasIndex(m => new { m.SessionId, m.MessageOrder });

        modelBuilder.Entity<ChatSessionMessage>()
            .Property(m => m.Role)
            .HasMaxLength(20)
            .IsRequired();

        modelBuilder.Entity<ChatSessionMessage>()
            .Property(m => m.Content)
            .IsRequired();
    }
}
