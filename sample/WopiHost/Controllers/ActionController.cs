#nullable enable
using Microsoft.AspNetCore.Mvc;
using WopiHost.Services;
using WopiHost.Discovery;
using Microsoft.Extensions.Options;
using WopiHost.Models.Configuration;
using WopiHost.Infrastructure;

namespace WopiHost.Controllers;

[ApiController]
[Route("api/[controller]")]
// The API key middleware will handle authentication for this controller
public class ActionController : ControllerBase
{
    private readonly IFileService _fileService;
    private readonly IJwtService _jwtService;
    private readonly IDiscoverer _wopiDiscoverer;
    private readonly WopiDiscoverySettings _discoverySettings;
    private readonly ILogger<ActionController> _logger;
    private readonly string _publicUrl;

    public ActionController(
        IFileService fileService,
        IJwtService jwtService,
        IDiscoverer wopiDiscoverer,
        IOptions<WopiDiscoverySettings> discoverySettings,
        ILogger<ActionController> logger,
        IConfiguration configuration)
    {
        _fileService = fileService;
        _jwtService = jwtService;
        _wopiDiscoverer = wopiDiscoverer;
        _discoverySettings = discoverySettings.Value;
        _logger = logger;
        _publicUrl = configuration.GetSection("WopiSettings:PublicHost").Value ?? "office.digifact.vn"; // Default public URL
    }

    /// <summary>
    /// Get action URL for viewing or editing a file
    /// </summary>
    /// <param name="id">File ID</param>
    /// <param name="action">Action type: view or edit</param>
    /// <param name="userId">User ID (optional, can be passed via query, header, or form)</param>
    /// <returns>URL for opening the file in Office Online</returns>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetActionUrl(int id, [FromQuery] string action = "view", [FromQuery] int? userId = null)
    {
        try
        {
            _logger.LogInformation("Headers: {@Headers}", Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString()));
            
            // Log API client information if available
            var apiClientName = HttpContext.GetApiClientName();
            if (apiClientName != null)
            {
                _logger.LogInformation("Request from API client: {ApiClientName}", apiClientName);
            }
            
            // Validate action parameter
            if (action != "view" && action != "edit")
            {
                return BadRequest("Action must be either 'view' or 'edit'");
            }

            // Get userId from multiple sources: query param, header, or form data
            var currentUserId = await GetUserIdFromRequest(userId);
            _logger.LogInformation("Processing request for file {FileId} with userId: {UserId}", id, currentUserId);

            // Get file from database
            var file = await _fileService.GetFileByIdAsync(id);
            if (file == null)
            {
                return NotFound("File not found");
            }

            // Check if file is supported by Office Online
            if (!file.IsOfficeDocument)
            {
                return BadRequest("File type not supported by Office Online");
            }

            // Generate access token with user information
            var accessToken = _jwtService.GenerateToken(file.Id, currentUserId);

            // Get discovery information - simplified approach since we don't have full discovery API
            var actionUrl = GetActionUrlFromDiscovery(file.FileExtension, action);
            if (string.IsNullOrEmpty(actionUrl))
            {
                return BadRequest($"No {action} action available for file type {file.FileExtension}");
            }

            // Build WOPI URLs
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            // Check for IIS ARR specific headers
            if (Request.Headers.ContainsKey("X-ARR-SSL") && Request.Headers.ContainsKey("Host"))
            {
                // If X-ARR-SSL is present, the connection is secure
                string scheme = "https";
                string host = Request.Headers["Host"]!;

                // If the X-Original-URL header is present, it may contain the host in the form of the full URL
                if (Request.Headers.ContainsKey("X-Original-For"))
                {
                    _logger.LogInformation("X-Original-For header found: {OriginalFor}", Request.Headers["X-Original-For"]!);
                }

                // Try to get the hostname from configuration or fall back to the incoming request
                var publicHostname = _publicUrl;

                baseUrl = $"{scheme}://{publicHostname}";
                _logger.LogInformation("Using baseUrl from IIS ARR headers: {BaseUrl}", baseUrl);
            }
            // Standard forwarded headers
            else if (Request.Headers.ContainsKey("X-Forwarded-Proto") && Request.Headers.ContainsKey("X-Forwarded-Host"))
            {
                baseUrl = $"{Request.Headers["X-Forwarded-Proto"]}://{Request.Headers["X-Forwarded-Host"]}";
                _logger.LogInformation("Using baseUrl from standard forwarded headers: {BaseUrl}", baseUrl);
            }
            var wopiSrc = $"{baseUrl}/wopi/files/{file.Id}";

            var finalUrl = $"{actionUrl}?WOPISrc={Uri.EscapeDataString(wopiSrc)}&access_token={accessToken}";

            var response = new
            {
                ActionUrl = finalUrl,
                AccessToken = accessToken,
                FileId = file.Id,
                FileName = file.FileName,
                Action = action,
                WopiSrc = wopiSrc
            };

            _logger.LogInformation("Generated {Action} URL for file {FileId}: {FileName}", action, id, file.FileName);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating action URL for file {FileId}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    private string? GetActionUrlFromDiscovery(string fileExtension, string action)
    {
        // This is a simplified implementation
        // In a real implementation, you would parse the actual WOPI discovery XML
        // and find the appropriate action URL based on file extension and action type
        
        var baseUrl = _discoverySettings.Url.Replace("/hosting/discovery", "");
        
        return fileExtension.ToLowerInvariant() switch
        {
            ".docx" or ".doc" => action == "edit" 
                ? $"{baseUrl}/we/wordeditorframe.aspx" 
                : $"{baseUrl}/wv/wordviewerframe.aspx",
            ".xlsx" or ".xls" => action == "edit" 
                ? $"{baseUrl}/x/_layouts/xlviewerinternal.aspx" 
                : $"{baseUrl}/x/_layouts/xlviewerinternal.aspx",
            ".pptx" or ".ppt" => action == "edit" 
                ? $"{baseUrl}/p/PowerPointFrame.aspx" 
                : $"{baseUrl}/p/PowerPointFrame.aspx",
            _ => null
        };
    }

    /// <summary>
    /// Get user ID from multiple request sources
    /// </summary>
    /// <param name="queryUserId">User ID from query parameter</param>
    /// <returns>User ID as string, defaults to "guest" if not found or invalid</returns>
    private async Task<string> GetUserIdFromRequest(int? queryUserId)
    {
        try
        {
            // Priority 1: Query parameter
            if (queryUserId.HasValue && queryUserId.Value > 0)
            {
                var userExists = await _fileService.UserExistsAsync(queryUserId.Value);
                if (userExists)
                {
                    _logger.LogInformation("User ID {UserId} found via query parameter", queryUserId.Value);
                    return queryUserId.Value.ToString();
                }
                _logger.LogWarning("User ID {UserId} from query parameter not found in database", queryUserId.Value);
            }

            // Priority 2: Header (X-User-ID)
            if (Request.Headers.ContainsKey("X-User-ID"))
            {
                var headerUserId = Request.Headers["X-User-ID"].ToString();
                if (int.TryParse(headerUserId, out var parsedHeaderUserId) && parsedHeaderUserId > 0)
                {
                    var userExists = await _fileService.UserExistsAsync(parsedHeaderUserId);
                    if (userExists)
                    {
                        _logger.LogInformation("User ID {UserId} found via X-User-ID header", parsedHeaderUserId);
                        return parsedHeaderUserId.ToString();
                    }
                    _logger.LogWarning("User ID {UserId} from X-User-ID header not found in database", parsedHeaderUserId);
                }
            }

            // Priority 3: Form data (if POST request)
            if (Request.HasFormContentType && Request.Form.ContainsKey("userId"))
            {
                var formUserId = Request.Form["userId"].ToString();
                if (int.TryParse(formUserId, out var parsedFormUserId) && parsedFormUserId > 0)
                {
                    var userExists = await _fileService.UserExistsAsync(parsedFormUserId);
                    if (userExists)
                    {
                        _logger.LogInformation("User ID {UserId} found via form data", parsedFormUserId);
                        return parsedFormUserId.ToString();
                    }
                    _logger.LogWarning("User ID {UserId} from form data not found in database", parsedFormUserId);
                }
            }

            // Default: guest user
            _logger.LogInformation("No valid user ID found, defaulting to guest user");
            return "guest";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while extracting user ID from request");
            return "guest";
        }
    }
}
