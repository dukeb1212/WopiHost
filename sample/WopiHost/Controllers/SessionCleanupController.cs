using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using WopiHost.Services;
using WopiHost.Abstractions;
using WopiHost.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;  

namespace WopiHost.Controllers;

/// <summary>
/// Session cleanup controller for handling WebView2 disposal scenarios
/// </summary>
[ApiController]
[Route("api/[controller]")]
[AllowAnonymous] // For testing - should be secured in production
public class SessionCleanupController : ControllerBase
{
    private readonly InMemoryWopiLockProvider _lockProvider;
    private readonly ILogger<SessionCleanupController> _logger;
    private readonly IWopiStorageProvider _storageProvider;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IJwtService _jwtService;

    private readonly string _publicUrl;

    public SessionCleanupController(
        IWopiLockProvider lockProvider,
        ILogger<SessionCleanupController> logger,
        IWopiStorageProvider storageProvider,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory = null,
        IJwtService jwtService = null)
    {
        _lockProvider = lockProvider as InMemoryWopiLockProvider
            ?? throw new InvalidOperationException("This controller requires InMemoryWopiLockProvider");
        _logger = logger;
        _storageProvider = storageProvider;
        _httpClientFactory = httpClientFactory;
        _jwtService = jwtService;
        _publicUrl = configuration.GetSection("WopiSettings:PublicHost").Value ?? "office.digifact.vn";
    }

    private string ExtractSessionIdFromLockId(string lockId)
    {
        try
        {
            if (string.IsNullOrEmpty(lockId)) return "unknown";

            // Handle Office Online Server format: {"S":"session-id","F":4}
            if (lockId.StartsWith("{") && lockId.Contains("\"S\":"))
            {
                var match = System.Text.RegularExpressions.Regex.Match(lockId, @"""S""\s*:\s*""([^""]+)""");
                if (match.Success)
                {
                    return match.Groups[1].Value;
                }
            }

            return lockId; // Fallback to full lockId
        }
        catch
        {
            return "unknown";
        }
    }

    /// <summary>
    /// Cleanup user sessions and start monitoring files for save completion
    /// New approach: Only cleanup sessions and monitor, don't force save
    /// </summary>
    /// <param name="userId">User ID to cleanup sessions for</param>
    /// <param name="saveTimeoutSeconds">Not used anymore but kept for backward compatibility</param>
    /// <returns>Cleanup and monitoring status</returns>
    [HttpPost("force-save-cleanup-user/{userId}")]
    public ActionResult<object> ForceSaveAndCleanupUserSessions(
        string userId,
        [FromQuery] int saveTimeoutSeconds = 30)
    {
        try
        {
            _logger.LogInformation("=== SESSION CLEANUP AND MONITORING === UserId: {UserId}", userId);

            // Get diagnostics before cleanup
            var diagnosticsBefore = _lockProvider.GetSessionDiagnostics();
            var totalSessionsBefore = diagnosticsBefore.Values.Sum(sessions => sessions.Count);


            // Clean ALL sessions for this user AFTER starting monitoring
            var cleanedCount = _lockProvider.CleanupStaleSessionsForUser(userId, TimeSpan.Zero);

            // Get diagnostics after cleanup
            var diagnosticsAfter = _lockProvider.GetSessionDiagnostics();
            var totalSessionsAfter = diagnosticsAfter.Values.Sum(sessions => sessions.Count);
            _logger.LogInformation("AFTER CLEANUP: {TotalSessions} total sessions across {FileCount} files", totalSessionsAfter, diagnosticsAfter.Count);

            var result = new
            {
                userId = userId,
                cleanedSessionsCount = cleanedCount,
                totalSessionsBefore = totalSessionsBefore,
                totalSessionsAfter = totalSessionsAfter,
                message = "Sessions cleaned up and file monitoring started. Files will be monitored for save completion.",
                timestamp = DateTimeOffset.UtcNow
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in session cleanup and monitoring for UserId: {UserId}", userId);
            return StatusCode(500, new { error = ex.Message, userId = userId, timestamp = DateTimeOffset.UtcNow });
        }
    }
}
