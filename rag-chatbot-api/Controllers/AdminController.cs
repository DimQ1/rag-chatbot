using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using rag_chatbot_api.Data;
using rag_chatbot_api.Dtos.Admin;
using rag_chatbot_api.Services;

namespace rag_chatbot_api.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/[controller]")]
public class AdminController(
    AppDbContext dbContext,
    IHostEnvironment hostEnvironment,
    IRagIndexService ragIndexService) : ControllerBase
{
    private readonly AppDbContext _dbContext = dbContext;
    private readonly IRagIndexService _ragIndexService = ragIndexService;
    private readonly string _knowledgeBasePath = Path.Combine(hostEnvironment.ContentRootPath, "KnowledgeBase");

    [HttpGet("users")]
    public async Task<ActionResult<IReadOnlyList<AdminUserResponse>>> GetUsers()
    {
        var users = await _dbContext.Users
            .OrderByDescending(u => u.CreatedAtUtc)
            .Select(u => new AdminUserResponse
            {
                Id = u.Id,
                Name = u.Name,
                Email = u.Email,
                Role = u.Role,
                HasPassword = !string.IsNullOrWhiteSpace(u.PasswordHash) && !string.IsNullOrWhiteSpace(u.PasswordSalt),
                CreatedAtUtc = u.CreatedAtUtc
            })
            .ToListAsync();

        return Ok(users);
    }

    [HttpPut("users/{id:guid}")]
    public async Task<ActionResult<AdminUserResponse>> UpdateUser(Guid id, AdminUpdateUserRequest request)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user is null)
        {
            return NotFound(new { message = "User not found." });
        }

        user.Name = request.Name.Trim();
        user.Role = request.Role.Trim();

        if (!string.IsNullOrWhiteSpace(request.NewPassword))
        {
            var (hash, salt) = PasswordService.HashPassword(request.NewPassword);
            user.PasswordHash = hash;
            user.PasswordSalt = salt;
        }

        await _dbContext.SaveChangesAsync();

        return Ok(new AdminUserResponse
        {
            Id = user.Id,
            Name = user.Name,
            Email = user.Email,
            Role = user.Role,
            HasPassword = !string.IsNullOrWhiteSpace(user.PasswordHash) && !string.IsNullOrWhiteSpace(user.PasswordSalt),
            CreatedAtUtc = user.CreatedAtUtc
        });
    }

    [HttpDelete("users/{id:guid}")]
    public async Task<ActionResult<object>> DeleteUser(Guid id)
    {
        var currentSub = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (Guid.TryParse(currentSub, out var currentUserId) && currentUserId == id)
        {
            return BadRequest(new { message = "You cannot delete your own admin account." });
        }

        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user is null)
        {
            return NotFound(new { message = "User not found." });
        }

        _dbContext.Users.Remove(user);
        await _dbContext.SaveChangesAsync();

        return Ok(new { message = "User deleted." });
    }

    [HttpGet("logs")]
    public async Task<ActionResult<AdminLogQueryResponse>> GetLogs([FromQuery] AdminLogQueryRequest request, CancellationToken cancellationToken)
    {
        var normalizedPage = Math.Max(request.Page, 1);
        var normalizedPageSize = Math.Clamp(request.PageSize, 10, 200);
        var search = request.Search?.Trim();
        var level = request.Level?.Trim();

        var logsQuery = _dbContext.ApplicationLogEntries
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var likePattern = $"%{search}%";
            logsQuery = logsQuery.Where(logEntry =>
                EF.Functions.Like(logEntry.Message, likePattern)
                || EF.Functions.Like(logEntry.Category, likePattern)
                || (logEntry.Exception != null && EF.Functions.Like(logEntry.Exception, likePattern))
                || (logEntry.RequestPath != null && EF.Functions.Like(logEntry.RequestPath, likePattern))
                || (logEntry.UserId != null && EF.Functions.Like(logEntry.UserId, likePattern)));
        }

        if (!string.IsNullOrWhiteSpace(level))
        {
            logsQuery = logsQuery.Where(logEntry => logEntry.Level == level);
        }

        var totalCount = await logsQuery.CountAsync(cancellationToken);
        var items = await logsQuery
            .OrderByDescending(logEntry => logEntry.TimestampUtc)
            .ThenByDescending(logEntry => logEntry.Id)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .Select(logEntry => new AdminLogEntryResponse
            {
                Id = logEntry.Id,
                TimestampUtc = logEntry.TimestampUtc,
                Level = logEntry.Level,
                Category = logEntry.Category,
                Message = logEntry.Message,
                Exception = logEntry.Exception,
                EventId = logEntry.EventId,
                EventName = logEntry.EventName,
                TraceId = logEntry.TraceId,
                RequestPath = logEntry.RequestPath,
                RequestMethod = logEntry.RequestMethod,
                UserId = logEntry.UserId
            })
            .ToListAsync(cancellationToken);

        return Ok(new AdminLogQueryResponse
        {
            Items = items,
            TotalCount = totalCount,
            Page = normalizedPage,
            PageSize = normalizedPageSize
        });
    }

    [HttpGet("documents")]
    public async Task<ActionResult<IReadOnlyList<AdminDocumentResponse>>> GetDocuments(CancellationToken cancellationToken)
    {
        await ImportLegacyKnowledgeBaseFilesAsync(cancellationToken);

        var documents = await _dbContext.RagSourceDocuments
            .AsNoTracking()
            .OrderByDescending(document => document.UpdatedAtUtc)
            .Select(document => new AdminDocumentResponse
            {
                Id = document.DocumentId,
                FileName = string.IsNullOrWhiteSpace(document.OriginalFileName) ? $"{document.DocumentId}.md" : document.OriginalFileName,
                Title = document.Title,
                Content = document.Content,
                UpdatedAtUtc = document.UpdatedAtUtc
            })
            .ToListAsync(cancellationToken);

        return Ok(documents);
    }

    [HttpPost("documents")]
    public async Task<ActionResult<AdminDocumentResponse>> CreateDocument(AdminUpsertDocumentRequest request, CancellationToken cancellationToken)
    {
        await ImportLegacyKnowledgeBaseFilesAsync(cancellationToken);
        var id = Slugify(request.Title);
        var exists = await _dbContext.RagSourceDocuments
            .AnyAsync(document => document.DocumentId == id, cancellationToken);
        if (exists)
        {
            return Conflict(new { message = "A document with a similar title already exists." });
        }

        var content = EnsureTitleHeading(request.Title, request.Content);
        var now = DateTime.UtcNow;

        var sourceDocument = new Models.RagSourceDocument
        {
            DocumentId = id,
            Title = request.Title.Trim(),
            OriginalFileName = $"{id}.md",
            Content = content,
            CreatedAtUtc = now,
            SourceUpdatedAtUtc = now,
            UpdatedAtUtc = now
        };

        _dbContext.RagSourceDocuments.Add(sourceDocument);
        await _dbContext.SaveChangesAsync(cancellationToken);

        WriteKnowledgeBaseFile(sourceDocument.DocumentId, sourceDocument.Content);
        _ = _ragIndexService.ReprocessDocumentAsync(id);

        return Ok(ToDocumentResponse(sourceDocument));
    }

    [HttpPut("documents/{id}")]
    public async Task<ActionResult<AdminDocumentResponse>> UpdateDocument(string id, AdminUpsertDocumentRequest request, CancellationToken cancellationToken)
    {
        var normalizedId = Slugify(id);
        var sourceDocument = await _dbContext.RagSourceDocuments
            .FirstOrDefaultAsync(document => document.DocumentId == normalizedId, cancellationToken);
        if (sourceDocument is null)
        {
            return NotFound(new { message = "Document not found." });
        }

        var nextId = Slugify(request.Title);
        var content = EnsureTitleHeading(request.Title, request.Content);

        if (!string.Equals(normalizedId, nextId, StringComparison.Ordinal)
            && await _dbContext.RagSourceDocuments.AnyAsync(document => document.DocumentId == nextId, cancellationToken))
        {
            return Conflict(new { message = "Another document already uses that title." });
        }

        var previousId = sourceDocument.DocumentId;
        sourceDocument.DocumentId = nextId;
        sourceDocument.Title = request.Title.Trim();
        sourceDocument.OriginalFileName = $"{nextId}.md";
        sourceDocument.Content = content;
        sourceDocument.SourceUpdatedAtUtc = DateTime.UtcNow;
        sourceDocument.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        if (!string.Equals(previousId, nextId, StringComparison.Ordinal))
        {
            DeleteKnowledgeBaseFile(previousId);
            _ = _ragIndexService.RemoveDocumentAsync(previousId);
        }

        WriteKnowledgeBaseFile(nextId, content);
        _ = _ragIndexService.ReprocessDocumentAsync(nextId);

        return Ok(ToDocumentResponse(sourceDocument));
    }

    [HttpDelete("documents/{id}")]
    public async Task<ActionResult<object>> DeleteDocument(string id, CancellationToken cancellationToken)
    {
        var normalizedId = Slugify(id);
        var sourceDocument = await _dbContext.RagSourceDocuments
            .FirstOrDefaultAsync(document => document.DocumentId == normalizedId, cancellationToken);
        if (sourceDocument is null)
        {
            return NotFound(new { message = "Document not found." });
        }

        _dbContext.RagSourceDocuments.Remove(sourceDocument);
        await _dbContext.SaveChangesAsync(cancellationToken);

        DeleteKnowledgeBaseFile(normalizedId);
        _ = _ragIndexService.RemoveDocumentAsync(normalizedId);
        return Ok(new { message = "Document deleted." });
    }

    [HttpPost("documents/reprocess")]
    public async Task<ActionResult<object>> ReprocessAllDocuments(CancellationToken cancellationToken)
    {
        var (processedCount, removedCount) = await _ragIndexService.ReprocessAllAsync(cancellationToken);
        return Ok(new
        {
            message = "Documents reprocessed.",
            processedCount,
            removedCount,
            refreshedAtUtc = DateTime.UtcNow
        });
    }

    [HttpPost("documents/{id}/reprocess")]
    public async Task<ActionResult<object>> ReprocessDocument(string id, CancellationToken cancellationToken)
    {
        var normalizedId = Slugify(id);
        var exists = await _dbContext.RagSourceDocuments
            .AnyAsync(document => document.DocumentId == normalizedId, cancellationToken);
        if (!exists)
        {
            return NotFound(new { message = "Document not found." });
        }

        var processed = await _ragIndexService.ReprocessDocumentAsync(normalizedId, cancellationToken);
        return Ok(new
        {
            message = processed ? "Document reprocessed." : "Document was already up to date.",
            documentId = normalizedId,
            refreshedAtUtc = DateTime.UtcNow
        });
    }

    [HttpGet("rag-configuration")]
    public async Task<ActionResult<AdminRagConfigurationResponse>> GetRagConfiguration()
    {
        var configuration = await _dbContext.RagRuntimeConfigurations.FirstOrDefaultAsync(c => c.Id == 1);
        if (configuration is null)
        {
            return NotFound(new { message = "RAG configuration not found." });
        }

        return Ok(ToRagConfigurationResponse(configuration));
    }

    [HttpPut("rag-configuration")]
    public async Task<ActionResult<AdminRagConfigurationResponse>> UpdateRagConfiguration(AdminUpdateRagConfigurationRequest request)
    {
        var configuration = await _dbContext.RagRuntimeConfigurations.FirstOrDefaultAsync(c => c.Id == 1);
        if (configuration is null)
        {
            configuration = new Models.RagRuntimeConfiguration { Id = 1 };
            _dbContext.RagRuntimeConfigurations.Add(configuration);
        }

        configuration.OpenAIBaseUrl = request.OpenAIBaseUrl.Trim();
        configuration.ModelId = request.ModelId.Trim();
        configuration.EmbeddingModelId = request.EmbeddingModelId.Trim();
        configuration.OpenAIApiKey = request.OpenAIApiKey.Trim();
        configuration.TopK = request.TopK;
        configuration.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();
        return Ok(ToRagConfigurationResponse(configuration));
    }

    private async Task ImportLegacyKnowledgeBaseFilesAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_knowledgeBasePath);

        var hasChanges = false;
        foreach (var filePath in Directory.EnumerateFiles(_knowledgeBasePath, "*.md", SearchOption.TopDirectoryOnly))
        {
            var documentId = Slugify(Path.GetFileNameWithoutExtension(filePath));
            var content = await System.IO.File.ReadAllTextAsync(filePath, cancellationToken);
            if (string.IsNullOrWhiteSpace(content))
            {
                continue;
            }

            var fileUpdatedAt = System.IO.File.GetLastWriteTimeUtc(filePath);
            var title = GetDocumentTitle(content, Path.GetFileNameWithoutExtension(filePath));

            var existing = await _dbContext.RagSourceDocuments
                .FirstOrDefaultAsync(document => document.DocumentId == documentId, cancellationToken);

            if (existing is null)
            {
                _dbContext.RagSourceDocuments.Add(new Models.RagSourceDocument
                {
                    DocumentId = documentId,
                    Title = title,
                    OriginalFileName = Path.GetFileName(filePath),
                    Content = content,
                    CreatedAtUtc = fileUpdatedAt,
                    SourceUpdatedAtUtc = fileUpdatedAt,
                    UpdatedAtUtc = fileUpdatedAt
                });

                hasChanges = true;
                continue;
            }

            if (existing.SourceUpdatedAtUtc >= fileUpdatedAt && string.Equals(existing.Content, content, StringComparison.Ordinal))
            {
                continue;
            }

            existing.Title = title;
            existing.OriginalFileName = Path.GetFileName(filePath);
            existing.Content = content;
            existing.SourceUpdatedAtUtc = fileUpdatedAt;
            existing.UpdatedAtUtc = DateTime.UtcNow;
            hasChanges = true;
        }

        if (hasChanges)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private static AdminDocumentResponse ToDocumentResponse(Models.RagSourceDocument sourceDocument)
    {
        return new AdminDocumentResponse
        {
            Id = sourceDocument.DocumentId,
            FileName = string.IsNullOrWhiteSpace(sourceDocument.OriginalFileName)
                ? $"{sourceDocument.DocumentId}.md"
                : sourceDocument.OriginalFileName,
            Title = sourceDocument.Title,
            Content = sourceDocument.Content,
            UpdatedAtUtc = sourceDocument.UpdatedAtUtc
        };
    }

    private void WriteKnowledgeBaseFile(string documentId, string content)
    {
        Directory.CreateDirectory(_knowledgeBasePath);
        var filePath = Path.Combine(_knowledgeBasePath, $"{documentId}.md");
        System.IO.File.WriteAllText(filePath, content);
    }

    private void DeleteKnowledgeBaseFile(string documentId)
    {
        var filePath = Path.Combine(_knowledgeBasePath, $"{documentId}.md");
        if (System.IO.File.Exists(filePath))
        {
            System.IO.File.Delete(filePath);
        }
    }

    private static string EnsureTitleHeading(string title, string content)
    {
        var trimmedContent = content.Trim();
        var heading = $"# {title.Trim()}";

        if (trimmedContent.StartsWith("# ", StringComparison.Ordinal))
        {
            var lines = trimmedContent.Split(["\r\n", "\n"], StringSplitOptions.None).ToList();
            lines[0] = heading;
            return string.Join(Environment.NewLine, lines).Trim() + Environment.NewLine;
        }

        return $"{heading}{Environment.NewLine}{Environment.NewLine}{trimmedContent}{Environment.NewLine}";
    }

    private static string GetDocumentTitle(string content, string fallback)
    {
        foreach (var line in content.Split(["\r\n", "\n"], StringSplitOptions.None))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("# ", StringComparison.Ordinal))
            {
                return trimmed[2..].Trim();
            }
        }

        return fallback;
    }

    private static string Slugify(string value)
    {
        var chars = value
            .Trim()
            .ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
            .ToArray();

        var collapsed = new string(chars);
        while (collapsed.Contains("--", StringComparison.Ordinal))
        {
            collapsed = collapsed.Replace("--", "-", StringComparison.Ordinal);
        }

        collapsed = collapsed.Trim('-');
        return string.IsNullOrWhiteSpace(collapsed) ? "document" : collapsed;
    }

    private static AdminRagConfigurationResponse ToRagConfigurationResponse(Models.RagRuntimeConfiguration configuration)
    {
        return new AdminRagConfigurationResponse
        {
            OpenAIBaseUrl = configuration.OpenAIBaseUrl,
            ModelId = configuration.ModelId,
            EmbeddingModelId = configuration.EmbeddingModelId,
            OpenAIApiKey = configuration.OpenAIApiKey,
            TopK = configuration.TopK,
            UpdatedAtUtc = configuration.UpdatedAtUtc
        };
    }

}
