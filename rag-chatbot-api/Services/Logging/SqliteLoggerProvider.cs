using System.Collections.Concurrent;
using Microsoft.AspNetCore.Http;

namespace rag_chatbot_api.Services.Logging;

public sealed class SqliteLoggerProvider(
    SqliteLogStore logStore,
    IHttpContextAccessor httpContextAccessor) : ILoggerProvider
{
    private readonly ConcurrentDictionary<string, SqliteLogger> _loggers = new(StringComparer.Ordinal);
    private readonly SqliteLogStore _logStore = logStore;
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;

    public ILogger CreateLogger(string categoryName)
    {
        return _loggers.GetOrAdd(
            categoryName,
            static (name, state) => new SqliteLogger(name, state.logStore, state.httpContextAccessor),
            (logStore: _logStore, httpContextAccessor: _httpContextAccessor));
    }

    public void Dispose()
    {
        _loggers.Clear();
    }
}
