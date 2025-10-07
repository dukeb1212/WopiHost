#nullable enable
using Microsoft.AspNetCore.Http;

namespace WopiHost.Infrastructure;

/// <summary>
/// Extension methods for HttpContext to work with API key information
/// </summary>
public static class HttpContextExtensions
{
    /// <summary>
    /// Gets the API client name from the HttpContext items
    /// </summary>
    /// <param name="httpContext">The current HTTP context</param>
    /// <returns>The client name if found, otherwise null</returns>
    public static string? GetApiClientName(this HttpContext httpContext)
    {
        if (httpContext.Items.TryGetValue("ApiClientName", out var clientName))
        {
            return clientName?.ToString();
        }
        
        return null;
    }
    
    /// <summary>
    /// Check if the request has a valid API key
    /// </summary>
    /// <param name="httpContext">The current HTTP context</param>
    /// <returns>True if the request has a valid API key, otherwise false</returns>
    public static bool HasValidApiKey(this HttpContext httpContext)
    {
        return httpContext.Items.ContainsKey("ApiClientName");
    }
}