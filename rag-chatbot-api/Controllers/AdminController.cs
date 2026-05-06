using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using rag_chatbot_api.Data;
using rag_chatbot_api.Dtos.Admin;
using rag_chatbot_api.Services;

namespace rag_chatbot_api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AdminController(
    AppDbContext dbContext,
    IHostEnvironment hostEnvironment,
    IRagIndexService ragIndexService) : ControllerBase
{
    private readonly AppDbContext _dbContext = dbContext;
    private readonly IRagIndexService _ragIndexService = ragIndexService;
    private readonly string _knowledgeBasePath = Path.Combine(hostEnvironment.ContentRootPath, "KnowledgeBase");

    [Authorize(Roles = "Admin")]
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

    [HttpGet("documents")]
    public ActionResult<IReadOnlyList<AdminDocumentResponse>> GetDocuments()
    {
        Directory.CreateDirectory(_knowledgeBasePath);

        var documents = Directory
            .EnumerateFiles(_knowledgeBasePath, "*.md", SearchOption.TopDirectoryOnly)
            .Select(filePath =>
            {
                var content = System.IO.File.ReadAllText(filePath);
                return new AdminDocumentResponse
                {
                    Id = Path.GetFileNameWithoutExtension(filePath),
                    FileName = Path.GetFileName(filePath),
                    Title = GetDocumentTitle(content, Path.GetFileNameWithoutExtension(filePath)),
                    Content = content,
                    UpdatedAtUtc = System.IO.File.GetLastWriteTimeUtc(filePath)
                };
            })
            .OrderByDescending(document => document.UpdatedAtUtc)
            .ToList();

        return Ok(documents);
    }

    [HttpPost("documents")]
    public ActionResult<AdminDocumentResponse> CreateDocument(AdminUpsertDocumentRequest request)
    {
        Directory.CreateDirectory(_knowledgeBasePath);

        var id = Slugify(request.Title);
        var filePath = Path.Combine(_knowledgeBasePath, $"{id}.md");
        if (System.IO.File.Exists(filePath))
        {
            return Conflict(new { message = "A document with a similar title already exists." });
        }

        var content = EnsureTitleHeading(request.Title, request.Content);
        System.IO.File.WriteAllText(filePath, content);
        _ = _ragIndexService.ReprocessDocumentAsync(id);

        return Ok(ToDocumentResponse(filePath, content));
    }

    [HttpPut("documents/{id}")]
    public ActionResult<AdminDocumentResponse> UpdateDocument(string id, AdminUpsertDocumentRequest request)
    {
        var normalizedId = Slugify(id);
        var existingPath = Path.Combine(_knowledgeBasePath, $"{normalizedId}.md");
        if (!System.IO.File.Exists(existingPath))
        {
            return NotFound(new { message = "Document not found." });
        }

        var nextId = Slugify(request.Title);
        var nextPath = Path.Combine(_knowledgeBasePath, $"{nextId}.md");
        var content = EnsureTitleHeading(request.Title, request.Content);

        if (!string.Equals(existingPath, nextPath, StringComparison.OrdinalIgnoreCase) && System.IO.File.Exists(nextPath))
        {
            return Conflict(new { message = "Another document already uses that title." });
        }

        if (!string.Equals(existingPath, nextPath, StringComparison.OrdinalIgnoreCase))
        {
            System.IO.File.Move(existingPath, nextPath);
            _ = _ragIndexService.RemoveDocumentAsync(normalizedId);
        }

        System.IO.File.WriteAllText(nextPath, content);
        _ = _ragIndexService.ReprocessDocumentAsync(nextId);

        return Ok(ToDocumentResponse(nextPath, content));
    }

    [HttpDelete("documents/{id}")]
    public ActionResult<object> DeleteDocument(string id)
    {
        var normalizedId = Slugify(id);
        var filePath = Path.Combine(_knowledgeBasePath, $"{normalizedId}.md");
        if (!System.IO.File.Exists(filePath))
        {
            return NotFound(new { message = "Document not found." });
        }

        System.IO.File.Delete(filePath);
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
        var exists = System.IO.File.Exists(Path.Combine(_knowledgeBasePath, $"{normalizedId}.md"));
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

    private static AdminDocumentResponse ToDocumentResponse(string filePath, string content)
    {
        return new AdminDocumentResponse
        {
            Id = Path.GetFileNameWithoutExtension(filePath),
            FileName = Path.GetFileName(filePath),
            Title = GetDocumentTitle(content, Path.GetFileNameWithoutExtension(filePath)),
            Content = content,
            UpdatedAtUtc = System.IO.File.GetLastWriteTimeUtc(filePath)
        };
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
