using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using rag_chatbot_api.Models;

namespace rag_chatbot_api.Services.Logging;

internal sealed class SqliteLogger(
    string categoryName,
    SqliteLogStore logStore,
    IHttpContextAccessor httpContextAccessor) : ILogger
{
    private readonly string _categoryName = categoryName;
    private readonly SqliteLogStore _logStore = logStore;
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return NullScope.Instance;
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return logLevel != LogLevel.None;
    }

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);

        if (!IsEnabled(logLevel))
        {
            return;
        }

        var message = formatter(state, exception);
        if (string.IsNullOrWhiteSpace(message) && exception is null)
        {
            return;
        }

        var httpContext = _httpContextAccessor.HttpContext;
        var userId = httpContext?.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? httpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        _logStore.Write(new ApplicationLogEntry
        {
            TimestampUtc = DateTime.UtcNow,
            Level = logLevel.ToString(),
            Category = Truncate(_categoryName, 256) ?? string.Empty,
            Message = Truncate(string.IsNullOrWhiteSpace(message) ? exception!.Message : message, 4000) ?? string.Empty,
            Exception = Truncate(exception?.ToString(), 16000),
            EventId = eventId.Id,
            EventName = Truncate(eventId.Name, 128),
            TraceId = Truncate(httpContext?.TraceIdentifier, 128),
            RequestPath = Truncate(httpContext?.Request.Path.Value, 256),
            RequestMethod = Truncate(httpContext?.Request.Method, 16),
            UserId = Truncate(userId, 64)
        });
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value;
        }

        return value[..maxLength];
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }
}
