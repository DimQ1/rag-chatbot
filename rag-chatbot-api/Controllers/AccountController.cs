using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using rag_chatbot_api.Data;
using rag_chatbot_api.Dtos.Account;
using rag_chatbot_api.Dtos.Auth;
using rag_chatbot_api.Models;
using rag_chatbot_api.Services;

namespace rag_chatbot_api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class AccountController(
    AppDbContext dbContext,
    ITokenService tokenService) : ControllerBase
{
    private readonly AppDbContext _dbContext = dbContext;
    private readonly ITokenService _tokenService = tokenService;

    [HttpGet("me")]
    public async Task<ActionResult<AuthResponse>> Me()
    {
        var user = await GetCurrentUserAsync();
        if (user is null)
        {
            return Unauthorized(new { message = "Invalid authentication context." });
        }

        return Ok(ToAuthResponse(user));
    }

    [HttpPut("profile")]
    public async Task<ActionResult<AuthResponse>> UpdateProfile(UpdateProfileRequest request)
    {
        var user = await GetCurrentUserAsync();
        if (user is null)
        {
            return Unauthorized(new { message = "Invalid authentication context." });
        }

        user.Name = request.Name.Trim();
        await _dbContext.SaveChangesAsync();

        return Ok(ToAuthResponse(user));
    }

    [HttpPut("password")]
    public async Task<ActionResult<object>> ChangePassword(ChangePasswordRequest request)
    {
        var user = await GetCurrentUserAsync();
        if (user is null)
        {
            return Unauthorized(new { message = "Invalid authentication context." });
        }

        if (string.IsNullOrWhiteSpace(user.PasswordHash) || string.IsNullOrWhiteSpace(user.PasswordSalt))
        {
            return Conflict(new { message = "This account uses social sign-in and does not have a password yet." });
        }

        if (!PasswordService.VerifyPassword(request.CurrentPassword, user.PasswordHash, user.PasswordSalt))
        {
            return Unauthorized(new { message = "Current password is invalid." });
        }

        var (hash, salt) = PasswordService.HashPassword(request.NewPassword);
        user.PasswordHash = hash;
        user.PasswordSalt = salt;
        await _dbContext.SaveChangesAsync();

        return Ok(new { message = "Password updated." });
    }

    private async Task<AppUser?> GetCurrentUserAsync()
    {
        var subject = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (!Guid.TryParse(subject, out var userId))
        {
            return null;
        }

        return await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId);
    }

    private AuthResponse ToAuthResponse(AppUser user)
    {
        var token = _tokenService.CreateToken(user);
        return new AuthResponse
        {
            Id = user.Id.ToString(),
            Email = user.Email,
            Name = user.Name,
            Token = token,
            Role = user.Role
        };
    }
}
