namespace Tecomm.Middleware;

/// <summary>
/// Custom middleware that logs every HTTP request to a rolling log file.
/// Captures: timestamp, method, path, query string, status code, elapsed ms,
/// client IP, and User-Agent.
/// </summary>
public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;
    private readonly string _logFilePath;
    private static readonly SemaphoreSlim _fileLock = new(1, 1);

    public RequestLoggingMiddleware(
        RequestDelegate next,
        ILogger<RequestLoggingMiddleware> logger,
        IConfiguration config)
    {
        _next        = next;
        _logger      = logger;
        _logFilePath = config["RequestLogging:LogFilePath"] ?? "logs/requests.log";

        // Ensure the log directory exists
        var dir = Path.GetDirectoryName(_logFilePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var start = DateTime.UtcNow;
        var sw    = System.Diagnostics.Stopwatch.StartNew();

        // Let the rest of the pipeline run
        await _next(context);

        sw.Stop();

        // Build log entry
        var entry = new RequestLogEntry
        {
            Timestamp   = start,
            Method      = context.Request.Method,
            Path        = context.Request.Path,
            QueryString = context.Request.QueryString.ToString(),
            StatusCode  = context.Response.StatusCode,
            ElapsedMs   = sw.ElapsedMilliseconds,
            ClientIp    = context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            UserAgent   = context.Request.Headers.UserAgent.ToString(),
            UserId      = context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "-"
        };

        // Write to console via ILogger (structured logging)
        _logger.LogInformation(
            "[REQUEST] {Method} {Path}{Query} → {StatusCode} ({ElapsedMs}ms) | IP:{ClientIp} | User:{UserId}",
            entry.Method, entry.Path, entry.QueryString,
            entry.StatusCode, entry.ElapsedMs,
            entry.ClientIp, entry.UserId);

        // Write to file asynchronously, thread-safe
        await WriteToFileAsync(entry);
    }

    private async Task WriteToFileAsync(RequestLogEntry entry)
    {
        var line = entry.ToLogLine();

        await _fileLock.WaitAsync();
        try
        {
            await File.AppendAllTextAsync(_logFilePath, line + Environment.NewLine);
        }
        catch (Exception ex)
        {
            // Never crash the app because of a logging failure
            _logger.LogWarning(ex, "Failed to write request log to file.");
        }
        finally
        {
            _fileLock.Release();
        }
    }
}

public class RequestLogEntry
{
    public DateTime Timestamp   { get; init; }
    public string   Method      { get; init; } = string.Empty;
    public string   Path        { get; init; } = string.Empty;
    public string   QueryString { get; init; } = string.Empty;
    public int      StatusCode  { get; init; }
    public long     ElapsedMs   { get; init; }
    public string   ClientIp    { get; init; } = string.Empty;
    public string   UserAgent   { get; init; } = string.Empty;
    public string   UserId      { get; init; } = "-";

    public string ToLogLine() =>
        $"{Timestamp:yyyy-MM-dd HH:mm:ss.fff} UTC | {Method,-6} | {StatusCode} | {ElapsedMs,5}ms " +
        $"| {Path}{QueryString} | IP:{ClientIp} | User:{UserId} | UA:{UserAgent}";
}

// ─── Extension method for clean registration in Program.cs ───────────────────
public static class RequestLoggingMiddlewareExtensions
{
    public static IApplicationBuilder UseRequestLogging(this IApplicationBuilder app)
        => app.UseMiddleware<RequestLoggingMiddleware>();
}
