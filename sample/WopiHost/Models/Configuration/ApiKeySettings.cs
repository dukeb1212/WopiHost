#nullable enable
namespace WopiHost.Models.Configuration;

/// <summary>
/// Settings for API key authentication
/// </summary>
public class ApiKeySettings
{
    /// <summary>
    /// Collection of API keys with their configurations
    /// </summary>
    public List<ApiKeyConfig> Keys { get; set; } = new();
}

/// <summary>
/// Configuration for a specific API key
/// </summary>
public class ApiKeyConfig
{
    /// <summary>
    /// The API key value
    /// </summary>
    public string Key { get; set; } = string.Empty;
    
    /// <summary>
    /// A descriptive name for the API key client
    /// </summary>
    public string ClientName { get; set; } = string.Empty;
    
    /// <summary>
    /// List of endpoints that this API key can access
    /// Use "*" to allow access to all endpoints
    /// </summary>
    public List<string> AllowedEndpoints { get; set; } = new();
}