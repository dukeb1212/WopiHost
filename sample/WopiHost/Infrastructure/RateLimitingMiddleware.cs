#nullable enable
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Net;
using System.Threading.RateLimiting;

namespace WopiHost.Infrastructure;

/// <summary>
/// Rate limiting middleware to prevent DDoS and bot scan attacks
/// </summary>
public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimitingMiddleware> _logger;
    private readonly RateLimiterOptions _options;
    private static readonly ConcurrentDictionary<string, RateLimitLease> _rateLimiters = new();

    /// <summary>
    /// Options for rate limiting
    /// </summary>
    public class RateLimiterOptions
    {
        /// <summary>
        /// Number of permitted requests per window
        /// </summary>
        public int PermitLimit { get; set; } = 100;
        
        /// <summary>
        /// Time window in seconds
        /// </summary>
        public int WindowInSeconds { get; set; } = 60;
        
        /// <summary>
        /// Maximum number of requests that can be queued when the limit is exceeded
        /// </summary>
        public int QueueLimit { get; set; } = 0;
        
        /// <summary>
        /// Whether to use client IP for rate limiting
        /// </summary>
        public bool UseClientIpForRateLimiting { get; set; } = true;
        
        /// <summary>
        /// List of paths to exclude from rate limiting
        /// </summary>
        public List<string> ExcludedPaths { get; set; } = new List<string>();
    }

    public RateLimitingMiddleware(
        RequestDelegate next,
        IOptions<RateLimiterOptions> options,
        ILogger<RateLimitingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
        _options = options.Value;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip rate limiting for excluded paths
        if (IsExcludedPath(context.Request.Path))
        {
            await _next(context);
            return;
        }

        // Get client identifier (IP address by default)
        var clientId = GetClientIdentifier(context);
        if (string.IsNullOrEmpty(clientId))
        {
            // Cannot identify client, proceed without rate limiting
            await _next(context);
            return;
        }

        // Get or create rate limiter for this client
        var rateLimiter = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
        {
            TokenLimit = _options.PermitLimit,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = _options.QueueLimit,
            ReplenishmentPeriod = TimeSpan.FromSeconds(_options.WindowInSeconds),
            TokensPerPeriod = _options.PermitLimit,
            AutoReplenishment = true
        });

        // Try to acquire a permit
        using var lease = await rateLimiter.AcquireAsync(1);
        if (lease.IsAcquired)
        {
            // Request is allowed, proceed to the next middleware
            await _next(context);
        }
        else
        {
            // Rate limit exceeded
            _logger.LogWarning("Rate limit exceeded for client {ClientId}", clientId);
            context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
            context.Response.Headers["Retry-After"] = _options.WindowInSeconds.ToString();
            await context.Response.WriteAsJsonAsync(new { error = "Too many requests. Please try again later." });
        }
    }

    private bool IsExcludedPath(PathString path)
    {
        return _options.ExcludedPaths.Any(p => path.StartsWithSegments(p, StringComparison.OrdinalIgnoreCase));
    }

    private string? GetClientIdentifier(HttpContext context)
    {
        if (_options.UseClientIpForRateLimiting)
        {
            // Check for forwarded headers first
            string? clientIp = null;
            
            // Check X-Forwarded-For header
            if (context.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor))
            {
                // X-Forwarded-For contains a list of IPs, the first one is the client IP
                var ips = forwardedFor.ToString().Split(',');
                if (ips.Length > 0)
                {
                    clientIp = ips[0].Trim();
                }
            }
            
            // Check X-Real-IP header
            if (string.IsNullOrEmpty(clientIp) && context.Request.Headers.TryGetValue("X-Real-IP", out var realIp))
            {
                clientIp = realIp.ToString();
            }
            
            // If we still don't have an IP, use the remote IP from the connection
            if (string.IsNullOrEmpty(clientIp))
            {
                clientIp = context.Connection.RemoteIpAddress?.ToString();
            }

            return clientIp;
        }

        // Otherwise use a fixed identifier
        return "global";
    }
}

/// <summary>
/// Extension methods for rate limiting
/// </summary>
public static class RateLimitingMiddlewareExtensions
{
    /// <summary>
    /// Add rate limiting services to the application
    /// </summary>
    public static IServiceCollection AddRateLimiting(this IServiceCollection services, Action<RateLimitingMiddleware.RateLimiterOptions> configureOptions)
    {
        services.Configure(configureOptions);
        return services;
    }

    /// <summary>
    /// Use rate limiting middleware in the application
    /// </summary>
    public static IApplicationBuilder UseRateLimiting(this IApplicationBuilder app)
    {
        return app.UseMiddleware<RateLimitingMiddleware>();
    }
}