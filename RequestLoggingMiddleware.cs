using System.Diagnostics;

public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // 1. Generate a short unique 8-character ID
        string correlationId = Guid.NewGuid().ToString("N")[..8];

        // 2. Log entry data before calling the next middleware
        _logger.LogInformation(
            "Request Started | ID: {CorrelationId} | Method: {Method} | Path: {Path}", 
            correlationId, context.Request.Method, context.Request.Path);

        // 3. Stamp the header EARLY so it is guaranteed to return on success or failure
        context.Response.Headers["X-Correlation-Id"] = correlationId;

        // 4. Start timing the request execution
        var stopwatch = Stopwatch.StartNew();

        // 5. Pass control downstream to the rest of the pipeline
        await _next(context);

        // 6. Stop timing once the pipeline execution returns here
        stopwatch.Stop();

        // 7. Log exit data with matching ID and performance time
        _logger.LogInformation(
            "Request Finished | ID: {CorrelationId} | Status: {StatusCode} | Elapsed: {ElapsedMs}ms", 
            correlationId, context.Response.StatusCode, stopwatch.ElapsedMilliseconds);
    }
}