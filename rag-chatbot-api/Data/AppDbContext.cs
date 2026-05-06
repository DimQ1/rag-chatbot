using Microsoft.EntityFrameworkCore;
using rag_chatbot_api.Models;

namespace rag_chatbot_api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<RagRuntimeConfiguration> RagRuntimeConfigurations => Set<RagRuntimeConfiguration>();
    public DbSet<RagVectorDocument> RagVectorDocuments => Set<RagVectorDocument>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
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
    }
}
