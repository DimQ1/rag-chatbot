using Google.Apis.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using rag_chatbot_api.Data;
using rag_chatbot_api.Dtos.Auth;
using rag_chatbot_api.Models;
using rag_chatbot_api.Options;
using rag_chatbot_api.Services;

namespace rag_chatbot_api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(
    AppDbContext dbContext,
    ITokenService tokenService,
    IOptions<GoogleAuthOptions> googleAuthOptions) : ControllerBase
{
    private const string DefaultUserRole = "User";
    private const string InvalidCredentialsMessage = "Invalid email or password.";
    private const string GoogleSignInOnlyMessage = "This account uses Google sign-in. Please log in with Google.";
    private const string ExistingPasswordAccountMessage = "An account with this email already exists. Please log in with your email and password.";

    private readonly AppDbContext _dbContext = dbContext;
    private readonly ITokenService _tokenService = tokenService;
    private readonly GoogleAuthOptions _googleAuthOptions = googleAuthOptions.Value;

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
    {
        var normalizedEmail = NormalizeEmail(request.Email);

        var existingUser = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail);
        if (existingUser is not null)
        {
            return Conflict(new { message = "Email is already registered." });
        }

        var (hash, salt) = PasswordService.HashPassword(request.Password);

        var user = new AppUser
        {
            Name = request.Name.Trim(),
            Email = normalizedEmail,
            PasswordHash = hash,
            PasswordSalt = salt,
            Role = DefaultUserRole
        };

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        return Ok(ToAuthResponse(user));
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        var normalizedEmail = NormalizeEmail(request.Email);

        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail);
        if (user is null)
        {
            return Unauthorized(new { message = InvalidCredentialsMessage });
        }

        if (string.IsNullOrEmpty(user.PasswordHash) || string.IsNullOrEmpty(user.PasswordSalt))
        {
            return Unauthorized(new { message = GoogleSignInOnlyMessage });
        }

        var validPassword = PasswordService.VerifyPassword(request.Password, user.PasswordHash, user.PasswordSalt);
        if (!validPassword)
        {
            return Unauthorized(new { message = InvalidCredentialsMessage });
        }

        return Ok(ToAuthResponse(user));
    }

    [HttpPost("google")]
    public async Task<ActionResult<AuthResponse>> GoogleLogin(GoogleLoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(_googleAuthOptions.ClientId) || _googleAuthOptions.ClientId.Contains("YOUR_"))
        {
            return BadRequest(new { message = "Google authentication is not configured on API side." });
        }

        GoogleJsonWebSignature.Payload payload;
        try
        {
            payload = await GoogleJsonWebSignature.ValidateAsync(request.IdToken, new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = [_googleAuthOptions.ClientId]
            });
        }
        catch
        {
            return Unauthorized(new { message = "Invalid Google token." });
        }

        var email = NormalizeEmail(payload.Email);
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == email);

        if (user is null)
        {
            user = CreateGoogleUser(payload, email);
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();
        }
        else if (!string.IsNullOrEmpty(user.PasswordHash))
        {
            return Conflict(new { message = ExistingPasswordAccountMessage });
        }

        return Ok(ToAuthResponse(user));
    }

    private static string NormalizeEmail(string email)
    {
        return email.Trim().ToLowerInvariant();
    }

    private static AppUser CreateGoogleUser(GoogleJsonWebSignature.Payload payload, string normalizedEmail)
    {
        return new AppUser
        {
            Name = payload.Name ?? payload.GivenName ?? "Google User",
            Email = normalizedEmail,
            Role = DefaultUserRole
        };
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
