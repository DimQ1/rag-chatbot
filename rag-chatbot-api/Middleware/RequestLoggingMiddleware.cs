using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using rag_chatbot_api.Models;
using rag_chatbot_api.Services.Logging;

namespace rag_chatbot_api.Middleware;

public sealed class RequestLoggingMiddleware(
    RequestDelegate next,
    ILogger<RequestLoggingMiddleware> logger,
    SqliteLogStore logStore)
{
    private readonly RequestDelegate _next = next;
    private readonly ILogger<RequestLoggingMiddleware> _logger = logger;
    private readonly SqliteLogStore _logStore = logStore;

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await _next(context);
            stopwatch.Stop();

            _logger.LogInformation(
                "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMilliseconds} ms",
                context.Request.Method,
                context.Request.Path.Value ?? "/",
                context.Response.StatusCode,
                stopwatch.ElapsedMilliseconds);

            await WriteLogEntryAsync(context, LogLevel.Information, null, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            _logger.LogError(
                ex,
                "HTTP {Method} {Path} failed in {ElapsedMilliseconds} ms",
                context.Request.Method,
                context.Request.Path.Value ?? "/",
                stopwatch.ElapsedMilliseconds);

            await WriteLogEntryAsync(context, LogLevel.Error, ex, stopwatch.ElapsedMilliseconds);

            throw;
        }
    }

    private async Task WriteLogEntryAsync(HttpContext context, LogLevel level, Exception? exception, long elapsedMilliseconds)
    {
        var userId = context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        var message = exception is null
            ? $"HTTP {context.Request.Method} {context.Request.Path.Value ?? "/"} responded {context.Response.StatusCode} in {elapsedMilliseconds} ms"
            : $"HTTP {context.Request.Method} {context.Request.Path.Value ?? "/"} failed in {elapsedMilliseconds} ms";

        await _logStore.WriteAsync(new ApplicationLogEntry
        {
            TimestampUtc = DateTime.UtcNow,
            Level = level.ToString(),
            Category = typeof(RequestLoggingMiddleware).FullName ?? nameof(RequestLoggingMiddleware),
            Message = message,
            Exception = exception?.ToString(),
            EventId = 0,
            TraceId = context.TraceIdentifier,
            RequestPath = context.Request.Path.Value,
            RequestMethod = context.Request.Method,
            UserId = userId
        }, context.RequestAborted);
    }
}
