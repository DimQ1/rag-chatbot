using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using rag_chatbot_api.Dtos.Rag;
using rag_chatbot_api.Services;

namespace rag_chatbot_api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RagController(IRagService ragService) : ControllerBase
{
    private readonly IRagService _ragService = ragService;

    [Authorize]
    [HttpPost("query")]
    public async Task<ActionResult<RagQueryResponse>> Query(RagQueryRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
        {
            return BadRequest(new { message = "Question is required." });
        }

        var response = await _ragService.QueryAsync(request.Question.Trim());

        return Ok(response);
    }
}
