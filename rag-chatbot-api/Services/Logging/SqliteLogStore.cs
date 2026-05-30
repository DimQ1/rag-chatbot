using Microsoft.Data.Sqlite;
using rag_chatbot_api.Models;

namespace rag_chatbot_api.Services.Logging;

public sealed class SqliteLogStore(IConfiguration configuration)
{
    private readonly string _connectionString = configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("DefaultConnection is not configured.");

    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public void Write(ApplicationLogEntry entry)
    {
        WriteAsync(entry, CancellationToken.None).GetAwaiter().GetResult();
    }

    public async Task WriteAsync(ApplicationLogEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            await EnsureSchemaAsync(cancellationToken);

            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                INSERT INTO "ApplicationLogEntries" (
                    "TimestampUtc",
                    "Level",
                    "Category",
                    "Message",
                    "Exception",
                    "EventId",
                    "EventName",
                    "TraceId",
                    "RequestPath",
                    "RequestMethod",
                    "UserId")
                VALUES (
                    $timestampUtc,
                    $level,
                    $category,
                    $message,
                    $exception,
                    $eventId,
                    $eventName,
                    $traceId,
                    $requestPath,
                    $requestMethod,
                    $userId);
                """;

            command.Parameters.AddWithValue("$timestampUtc", entry.TimestampUtc);
            command.Parameters.AddWithValue("$level", entry.Level);
            command.Parameters.AddWithValue("$category", entry.Category);
            command.Parameters.AddWithValue("$message", entry.Message);
            command.Parameters.AddWithValue("$exception", (object?)entry.Exception ?? DBNull.Value);
            command.Parameters.AddWithValue("$eventId", entry.EventId);
            command.Parameters.AddWithValue("$eventName", (object?)entry.EventName ?? DBNull.Value);
            command.Parameters.AddWithValue("$traceId", (object?)entry.TraceId ?? DBNull.Value);
            command.Parameters.AddWithValue("$requestPath", (object?)entry.RequestPath ?? DBNull.Value);
            command.Parameters.AddWithValue("$requestMethod", (object?)entry.RequestMethod ?? DBNull.Value);
            command.Parameters.AddWithValue("$userId", (object?)entry.UserId ?? DBNull.Value);

            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private async Task EnsureSchemaAsync(CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            CREATE TABLE IF NOT EXISTS "ApplicationLogEntries" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_ApplicationLogEntries" PRIMARY KEY AUTOINCREMENT,
                "TimestampUtc" TEXT NOT NULL,
                "Level" TEXT NOT NULL,
                "Category" TEXT NOT NULL,
                "Message" TEXT NOT NULL,
                "Exception" TEXT NULL,
                "EventId" INTEGER NOT NULL,
                "EventName" TEXT NULL,
                "TraceId" TEXT NULL,
                "RequestPath" TEXT NULL,
                "RequestMethod" TEXT NULL,
                "UserId" TEXT NULL
            );

            CREATE INDEX IF NOT EXISTS "IX_ApplicationLogEntries_TimestampUtc"
                ON "ApplicationLogEntries" ("TimestampUtc");

            CREATE INDEX IF NOT EXISTS "IX_ApplicationLogEntries_Level_TimestampUtc"
                ON "ApplicationLogEntries" ("Level", "TimestampUtc");
            """;

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
