using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using rag_chatbot_api.Dtos.ChatSession;
using rag_chatbot_api.Services;

namespace rag_chatbot_api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ChatSessionController(
    IChatSessionService chatSessionService,
    IRagService ragService,
    ILogger<ChatSessionController> logger) : ControllerBase
{
    private readonly IChatSessionService _chatSessionService = chatSessionService;
    private readonly IRagService _ragService = ragService;
    private readonly ILogger<ChatSessionController> _logger = logger;

    private Guid GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdClaim?.Value, out var userId) ? userId : Guid.Empty;
    }

    [HttpPost("create")]
    public async Task<ActionResult<ChatSessionResponse>> CreateSession(
        [FromBody] CreateChatSessionRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty)
            return Unauthorized(new { message = "Invalid user context." });

        try
        {
            var session = await _chatSessionService.CreateSessionAsync(userId, request.InitialQuestion, cancellationToken);

            // If there's an initial question, add it as the first user message
            if (!string.IsNullOrWhiteSpace(request.InitialQuestion))
            {
                await _chatSessionService.AddMessageToSessionAsync(
                    session.Id,
                    "user",
                    request.InitialQuestion,
                    null,
                    cancellationToken);
            }

            var response = new ChatSessionResponse
            {
                Id = session.Id,
                Topic = session.Topic,
                IsPinned = session.IsPinned,
                CreatedAtUtc = session.CreatedAtUtc,
                UpdatedAtUtc = session.UpdatedAtUtc,
                MessageCount = 0
            };

            return CreatedAtAction(nameof(GetSession), new { sessionId = session.Id }, response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating chat session");
            return StatusCode(500, new { message = "Failed to create chat session." });
        }
    }

    [HttpGet]
    public async Task<ActionResult<List<ChatSessionResponse>>> GetSessions(CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty)
            return Unauthorized(new { message = "Invalid user context." });

        try
        {
            var sessions = await _chatSessionService.GetUserSessionsAsync(userId, cancellationToken);
            return Ok(sessions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving chat sessions");
            return StatusCode(500, new { message = "Failed to retrieve chat sessions." });
        }
    }

    [HttpGet("{sessionId}")]
    public async Task<ActionResult<ChatSessionDetailResponse>> GetSession(
        [FromRoute] Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty)
            return Unauthorized(new { message = "Invalid user context." });

        try
        {
            var session = await _chatSessionService.GetSessionDetailAsync(sessionId, userId, cancellationToken);
            if (session == null)
                return NotFound(new { message = "Chat session not found." });

            return Ok(session);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving chat session detail");
            return StatusCode(500, new { message = "Failed to retrieve chat session." });
        }
    }

    [HttpPost("{sessionId}/add-message")]
    public async Task<ActionResult> AddMessage(
        [FromRoute] Guid sessionId,
        [FromBody] AddMessageToChatSessionRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty)
            return Unauthorized(new { message = "Invalid user context." });

        if (string.IsNullOrWhiteSpace(request.Question))
            return BadRequest(new { message = "Question is required." });

        try
        {
            // Verify session belongs to user
            var session = await _chatSessionService.GetSessionAsync(sessionId, userId, cancellationToken);
            if (session == null)
                return NotFound(new { message = "Chat session not found." });

            // Add user message
            await _chatSessionService.AddMessageToSessionAsync(
                sessionId,
                "user",
                request.Question,
                null,
                cancellationToken);

            // Get RAG response
            var ragResponse = await _ragService.QueryAsync(request.Question, cancellationToken);

            // Convert RagSource to ChatMessageSource
            var sources = ragResponse.Sources?.Select(s => new ChatMessageSource
            {
                Title = s.Title,
                Url = s.Url
            }).ToList();

            // Add assistant message
            await _chatSessionService.AddMessageToSessionAsync(
                sessionId,
                "assistant",
                ragResponse.Answer,
                sources,
                cancellationToken);

            // Generate topic if this is the first message exchange
            if (session.MessageCount == 0)
            {
                var topic = await _chatSessionService.GenerateTopicAsync(
                    request.Question,
                    ragResponse.Answer,
                    cancellationToken);

                await _chatSessionService.RenameSessionAsync(sessionId, userId, topic, cancellationToken);
            }

            return Ok(new { message = "Message added successfully." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding message to session");
            return StatusCode(500, new { message = "Failed to add message to session." });
        }
    }

    [HttpPatch("{sessionId}/rename")]
    public async Task<ActionResult> RenameSession(
        [FromRoute] Guid sessionId,
        [FromBody] RenameChatSessionRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty)
            return Unauthorized(new { message = "Invalid user context." });

        if (string.IsNullOrWhiteSpace(request.Topic))
            return BadRequest(new { message = "Topic is required." });

        try
        {
            var success = await _chatSessionService.RenameSessionAsync(
                sessionId,
                userId,
                request.Topic,
                cancellationToken);

            if (!success)
                return NotFound(new { message = "Chat session not found." });

            return Ok(new { message = "Session renamed successfully." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error renaming session");
            return StatusCode(500, new { message = "Failed to rename session." });
        }
    }

    [HttpPatch("{sessionId}/pin")]
    public async Task<ActionResult> PinSession(
        [FromRoute] Guid sessionId,
        [FromBody] PinChatSessionRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty)
            return Unauthorized(new { message = "Invalid user context." });

        try
        {
            var success = await _chatSessionService.PinSessionAsync(
                sessionId,
                userId,
                request.IsPinned,
                cancellationToken);

            if (!success)
                return NotFound(new { message = "Chat session not found." });

            return Ok(new { message = request.IsPinned ? "Session pinned successfully." : "Session unpinned successfully." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error pinning session");
            return StatusCode(500, new { message = "Failed to pin session." });
        }
    }

    [HttpDelete("{sessionId}")]
    public async Task<ActionResult> DeleteSession(
        [FromRoute] Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty)
            return Unauthorized(new { message = "Invalid user context." });

        try
        {
            var success = await _chatSessionService.DeleteSessionAsync(sessionId, userId, cancellationToken);
            if (!success)
                return NotFound(new { message = "Chat session not found." });

            return Ok(new { message = "Session deleted successfully." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting session");
            return StatusCode(500, new { message = "Failed to delete session." });
        }
    }
}
