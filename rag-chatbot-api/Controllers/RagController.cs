using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using rag_chatbot_api.Data;
using rag_chatbot_api.Dtos.Rag;
using rag_chatbot_api.Services;

namespace rag_chatbot_api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RagController(IRagService ragService, AppDbContext dbContext) : ControllerBase
{
    private readonly IRagService _ragService = ragService;
    private readonly AppDbContext _dbContext = dbContext;

    [Authorize]
    [HttpPost("query")]
    public async Task<ActionResult<RagQueryResponse>> Query(RagQueryRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
        {
            return BadRequest(new { message = "Question is required." });
        }

        var response = await _ragService.QueryAsync(
            request.Question.Trim(),
            includeReasoning: request.IncludeReasoning,
            cancellationToken: HttpContext.RequestAborted);

        return Ok(response);
    }

    [Authorize]
    [HttpGet("documents/{documentId}")]
    public async Task<ActionResult<RagDocumentResponse>> GetDocument(
        [FromRoute] string documentId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(documentId))
        {
            return BadRequest(new { message = "Document id is required." });
        }

        var sourceDocument = await _dbContext.RagSourceDocuments
            .AsNoTracking()
            .FirstOrDefaultAsync(document => document.DocumentId == documentId, cancellationToken);

        if (sourceDocument is null)
        {
            return NotFound(new { message = "Document not found." });
        }

        return Ok(new RagDocumentResponse
        {
            DocumentId = sourceDocument.DocumentId,
            Title = sourceDocument.Title,
            Content = sourceDocument.Content,
            SourceUpdatedAtUtc = sourceDocument.SourceUpdatedAtUtc
        });
    }
}
