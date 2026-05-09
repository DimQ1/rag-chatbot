using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using rag_chatbot_api.Data;
using rag_chatbot_api.Models;
using rag_chatbot_api.Options;
using rag_chatbot_api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<GoogleAuthOptions>(builder.Configuration.GetSection(GoogleAuthOptions.SectionName));
builder.Services.Configure<RagOptions>(builder.Configuration.GetSection(RagOptions.SectionName));
builder.Services.Configure<AdminOptions>(builder.Configuration.GetSection(AdminOptions.SectionName));

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? throw new InvalidOperationException("Jwt configuration is missing.");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IRagIndexService, RagIndexService>();
builder.Services.AddScoped<IRagService, RagService>();
builder.Services.AddScoped<IChatSessionService, ChatSessionService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AngularApp", policy =>
    {
        policy.WithOrigins("http://localhost:4200")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SecretKey))
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var serviceProvider = scope.ServiceProvider;
    var dbContext = serviceProvider.GetRequiredService<AppDbContext>();
    var adminOptions = serviceProvider.GetRequiredService<IOptions<AdminOptions>>().Value;
    var ragOptions = serviceProvider.GetRequiredService<IOptions<RagOptions>>().Value;

    dbContext.Database.EnsureCreated();
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
    EnsureEmbeddingModelColumn(dbContext);
    EnsureSourceDocumentTable(dbContext);
    EnsureVectorDocumentTable(dbContext);
    
    // Create ChatSessions and ChatSessionMessages tables
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
    
    // Create indexes
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
    
    await EnsureRagConfigurationAsync(dbContext, ragOptions);
    await EnsureAdminUserAsync(dbContext, adminOptions);
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("AngularApp");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

static async Task EnsureAdminUserAsync(AppDbContext dbContext, AdminOptions adminOptions)
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
        ? "Administrator"
        : adminOptions.Name.Trim();

    if (user is null)
    {
        dbContext.Users.Add(new AppUser
        {
            Email = normalizedEmail,
            Name = normalizedName,
            PasswordHash = hash,
            PasswordSalt = salt,
            Role = "Admin"
        });

        await dbContext.SaveChangesAsync();
        return;
    }

    user.Role = "Admin";
    user.Name = normalizedName;

    if (adminOptions.ResetPasswordOnStartup || string.IsNullOrWhiteSpace(user.PasswordHash) || string.IsNullOrWhiteSpace(user.PasswordSalt))
    {
        user.PasswordHash = hash;
        user.PasswordSalt = salt;
    }

    await dbContext.SaveChangesAsync();
}

static async Task EnsureRagConfigurationAsync(AppDbContext dbContext, RagOptions ragOptions)
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

static void EnsureEmbeddingModelColumn(AppDbContext dbContext)
{
    try
    {
        dbContext.Database.ExecuteSqlRaw(
            "ALTER TABLE RagRuntimeConfigurations ADD COLUMN EmbeddingModelId TEXT NOT NULL DEFAULT 'text-embedding-3-small';");
    }
    catch
    {
        // Ignore when the column already exists in SQLite.
    }
}

static void EnsureVectorDocumentTable(AppDbContext dbContext)
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

    try
    {
        dbContext.Database.ExecuteSqlRaw("ALTER TABLE RagVectorDocuments ADD COLUMN EmbeddingJson TEXT NULL;");
    }
    catch
    {
        // Ignore when the column already exists.
    }

    try
    {
        dbContext.Database.ExecuteSqlRaw("ALTER TABLE RagVectorDocuments ADD COLUMN EmbeddingModelId TEXT NULL;");
    }
    catch
    {
        // Ignore when the column already exists.
    }
}

static void EnsureSourceDocumentTable(AppDbContext dbContext)
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
