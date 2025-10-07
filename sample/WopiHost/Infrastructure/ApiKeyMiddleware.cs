#nullable enable
using Microsoft.Extensions.Options;
using System.Net;
using WopiHost.Models.Configuration;

namespace WopiHost.Infrastructure;

public class ApiKeyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ApiKeyMiddleware> _logger;
    private readonly ApiKeySettings _apiKeySettings;

    public ApiKeyMiddleware(
        RequestDelegate next,
        IOptions<ApiKeySettings> apiKeySettings,
        ILogger<ApiKeyMiddleware> logger)
    {
        _next = next;
        _logger = logger;
        _apiKeySettings = apiKeySettings.Value;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip API key validation for WOPI endpoints as they use their own auth mechanism
        if (context.Request.Path.StartsWithSegments("/wopi"))
        {
            _logger.LogDebug("Skipping API key validation for WOPI endpoint: {Path}", context.Request.Path);
            await _next(context);
            return;
        }

        // Handle access_token in query string as a special case for Office Online
        if (context.Request.Query.ContainsKey("access_token"))
        {
            _logger.LogDebug("Request contains access_token, likely a WOPI request");
            await _next(context);
            return;
        }

        if (!TryGetApiKey(context, out string apiKey))
        {
            _logger.LogWarning("API key was not provided in the request");
            context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "API key is required" });
            return;
        }

        var apiKeyConfig = _apiKeySettings.Keys.FirstOrDefault(k => k.Key == apiKey);
        if (apiKeyConfig == null)
        {
            _logger.LogWarning("Invalid API key provided: {ApiKey}", apiKey);
            context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "Invalid API key" });
            return;
        }

        // Check if the endpoint is allowed
        var requestPath = context.Request.Path.Value?.ToLowerInvariant();
        if (requestPath != null && !IsEndpointAllowed(requestPath, apiKeyConfig.AllowedEndpoints))
        {
            _logger.LogWarning("API key {ApiKey} is not authorized to access {Path}", apiKey, requestPath);
            context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
            await context.Response.WriteAsJsonAsync(new { error = "API key is not authorized to access this endpoint" });
            return;
        }

        // Add client info to the HttpContext for logging or usage in controllers
        context.Items["ApiClientName"] = apiKeyConfig.ClientName;
        
        await _next(context);
    }

    private bool TryGetApiKey(HttpContext context, out string apiKey)
    {
        apiKey = string.Empty;
        
        // Check header
        if (context.Request.Headers.TryGetValue("X-API-Key", out var headerApiKey) && !string.IsNullOrEmpty(headerApiKey))
        {
            apiKey = headerApiKey.ToString();
            return true;
        }
        
        // Check query string
        if (context.Request.Query.TryGetValue("api-key", out var queryApiKey) && !string.IsNullOrEmpty(queryApiKey))
        {
            apiKey = queryApiKey.ToString();
            return true;
        }
        
        return false;
    }

    private bool IsEndpointAllowed(string requestPath, IEnumerable<string> allowedEndpoints)
    {
        // If any endpoint has "*", it means all endpoints are allowed
        if (allowedEndpoints.Any(e => e == "*"))
        {
            return true;
        }

        // Check if any of the allowed endpoints match the request path
        return allowedEndpoints.Any(endpoint =>
        {
            if (endpoint.EndsWith("/*"))
            {
                // Handle prefix matching (e.g., "/api/files/*")
                var prefix = endpoint.TrimEnd('*').TrimEnd('/');
                return requestPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
            }
            
            // Exact match
            return requestPath.Equals(endpoint, StringComparison.OrdinalIgnoreCase);
        });
    }
}

// Extension methods for services and application
public static class ApiKeyMiddlewareExtensions
{
    public static IServiceCollection AddApiKeyAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ApiKeySettings>(configuration.GetSection("ApiKeySettings"));
        return services;
    }

    public static IApplicationBuilder UseApiKeyAuthentication(this IApplicationBuilder app)
    {
        return app.UseMiddleware<ApiKeyMiddleware>();
    }
}