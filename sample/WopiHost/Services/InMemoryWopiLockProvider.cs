// File: Services/InMemoryWopiLockProvider.cs
#nullable enable
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using WopiHost.Abstractions;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace WopiHost.Services;

public class InMemoryWopiLockProvider : IWopiLockProvider
{
    // Change from single lock per file to multiple sessions per file
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, WopiLockInfo>> _fileLocks = new();
    private readonly ConcurrentDictionary<string, string> _sessionToUser = new(); // Track session -> user mapping
    private readonly ILogger<InMemoryWopiLockProvider>? _logger;
    private readonly IHttpContextAccessor? _httpContextAccessor;

    public InMemoryWopiLockProvider(ILogger<InMemoryWopiLockProvider>? logger = null, IHttpContextAccessor? httpContextAccessor = null)
    {
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
        _logger?.LogInformation("InMemoryWopiLockProvider initialized");
    }

    /// <summary>
    /// Extracts the session ID from Office Online Server lock IDs.
    /// OOS sends complex JSON lock IDs like: {"S":"session-id","F":4,"E":2,"M":"...","P":"..."}
    /// We compare based on the session ID ("S" property) for lock compatibility.
    /// </summary>
    private string ExtractSessionId(string lockId)
    {
        if (string.IsNullOrEmpty(lockId))
            return lockId;

        try
        {
            // Try to parse as JSON to extract session ID
            using var document = JsonDocument.Parse(lockId);
            if (document.RootElement.TryGetProperty("S", out var sessionProperty))
            {
                return sessionProperty.GetString() ?? lockId;
            }
        }
        catch (JsonException)
        {
            // If it's not JSON, treat as regular string
            _logger?.LogDebug("LockId is not JSON format: {LockId}", lockId);
        }

        return lockId;
    }

    /// <summary>
    /// Extracts user ID from Office Online Server lock IDs.
    /// Some implementations include user info in lock metadata.
    /// </summary>
    private string? ExtractUserId(string lockId)
    {
        if (string.IsNullOrEmpty(lockId))
            return null;

        try
        {
            // Try to parse as JSON to extract user ID if available
            using var document = JsonDocument.Parse(lockId);
            if (document.RootElement.TryGetProperty("U", out var userProperty))
            {
                return userProperty.GetString();
            }
            // Some systems might use different property names
            if (document.RootElement.TryGetProperty("UserId", out var userIdProperty))
            {
                return userIdProperty.GetString();
            }
        }
        catch (JsonException)
        {
            // If it's not JSON, can't extract user ID
        }

        return null;
    }

    /// <summary>
    /// Extracts user ID from current HTTP context JWT token
    /// </summary>
    private string? GetCurrentUserId()
    {
        try
        {
            var user = _httpContextAccessor?.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated == true)
            {
                // Try different claim types that might contain user ID
                var userId = user.FindFirst("user_id")?.Value ??
                           user.FindFirst("nameid")?.Value ??
                           user.FindFirst("sub")?.Value ??
                           user.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;
                
                if (!string.IsNullOrEmpty(userId))
                {
                    _logger?.LogDebug("Extracted userId from JWT: {UserId}", userId);
                    return userId;
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to extract userId from JWT token");
        }

        return null;
    }

    /// <summary>
    /// Compares two lock IDs for compatibility.
    /// For Office Online Server, locks are compatible if they have the same session ID.
    /// </summary>
    private bool AreLocksCompatible(string lockId1, string lockId2)
    {
        if (string.IsNullOrEmpty(lockId1) || string.IsNullOrEmpty(lockId2))
            return false;

        // First try exact match
        if (lockId1 == lockId2)
            return true;

        // Extract session IDs and compare
        var sessionId1 = ExtractSessionId(lockId1);
        var sessionId2 = ExtractSessionId(lockId2);

        var compatible = sessionId1 == sessionId2;
        
        if (compatible && lockId1 != lockId2)
        {
            _logger?.LogDebug("Lock IDs are compatible - same session ID: {SessionId}", sessionId1);
        }

        return compatible;
    }

    public bool TryGetLock(string fileId, [NotNullWhen(true)] out WopiLockInfo? lockInfo)
    {
        _logger?.LogDebug("TryGetLock called for FileId: {FileId}", fileId);
        
        lockInfo = null;
        
        // Get all sessions for this file
        if (_fileLocks.TryGetValue(fileId, out var fileSessions))
        {
            // Clean up expired sessions first
            var expiredSessions = fileSessions.Where(kvp => kvp.Value.Expired).ToList();
            foreach (var expired in expiredSessions)
            {
                fileSessions.TryRemove(expired.Key, out _);
                _logger?.LogInformation("Removed expired session {SessionId} for FileId: {FileId}", expired.Key, fileId);
            }
            
            // Return the first valid session (for WOPI compatibility)
            var activeSessions = fileSessions.Values.Where(l => !l.Expired).ToList();
            if (activeSessions.Any())
            {
                lockInfo = activeSessions.First();
                _logger?.LogDebug("Found {Count} active session(s) for FileId: {FileId}, returning first: {LockId}", 
                    activeSessions.Count, fileId, lockInfo.LockId);
                return true;
            }
            
            // Remove empty file entry if no active sessions
            if (fileSessions.IsEmpty)
            {
                _fileLocks.TryRemove(fileId, out _);
            }
        }
        
        _logger?.LogDebug("No active sessions found for FileId: {FileId}", fileId);
        return false;
    }

    public WopiLockInfo? AddLock(string fileId, string lockId)
    {
        _logger?.LogInformation("=== ADD LOCK DEBUG === FileId: {FileId}, LockId: {LockId}", fileId, lockId);

        var sessionId = ExtractSessionId(lockId);
        var userId = GetCurrentUserId() ?? ExtractUserId(lockId); // Try JWT first, then lockId
        
        _logger?.LogInformation("Extracted SessionId: {SessionId}, UserId: {UserId}", sessionId, userId);
        
        // Track session to user mapping
        if (!string.IsNullOrEmpty(userId))
        {
            _sessionToUser.AddOrUpdate(sessionId, userId, (key, oldValue) => userId);
            _logger?.LogInformation("Mapped SessionId {SessionId} to UserId {UserId}", sessionId, userId);
            
            // Cleanup stale sessions for this user first
            // This helps handle WebView2 dispose/recreate scenarios
            var cleanedCount = CleanupStaleSessionsForUser(userId, TimeSpan.FromMinutes(1));
            if (cleanedCount > 0)
            {
                _logger?.LogInformation("Auto-cleaned {Count} stale sessions for UserId: {UserId} before adding new lock", cleanedCount, userId);
            }
        }
        else
        {
            _logger?.LogWarning("Could not determine userId for session: {SessionId}, lockId: {LockId}", sessionId, lockId);
        }
        
        // Get or create sessions dictionary for this file
        var fileSessions = _fileLocks.GetOrAdd(fileId, _ => new ConcurrentDictionary<string, WopiLockInfo>());
        
        // Clean up expired sessions first
        var expiredSessions = fileSessions.Where(kvp => kvp.Value.Expired).ToList();
        foreach (var expired in expiredSessions)
        {
            fileSessions.TryRemove(expired.Key, out _);
            _logger?.LogInformation("Removed expired session {SessionId} for FileId: {FileId}", expired.Key, fileId);
        }
        
        // Check if this session already has a lock
        if (fileSessions.TryGetValue(sessionId, out var existingSessionLock))
        {
            // Update existing session lock
            var updatedLock = existingSessionLock with { LockId = lockId, DateCreated = DateTimeOffset.UtcNow };
            if (fileSessions.TryUpdate(sessionId, updatedLock, existingSessionLock))
            {
                _logger?.LogInformation("Updated existing session lock for FileId: {FileId}, SessionId: {SessionId}, LockId: {LockId}", 
                    fileId, sessionId, lockId);
                return updatedLock;
            }
            return null;
        }
        
        // For simplicity, allow multiple sessions but log warnings for different sessions
        var activeSessions = fileSessions.Values.Where(l => !l.Expired).ToList();
        if (activeSessions.Any())
        {
            _logger?.LogWarning("File {FileId} already has {Count} active session(s), adding new session {SessionId}", 
                fileId, activeSessions.Count, sessionId);
        }
        
        // Create new session lock
        var newLock = new WopiLockInfo
        {
            FileId = fileId,
            LockId = lockId,
            DateCreated = DateTimeOffset.UtcNow
        };

        if (fileSessions.TryAdd(sessionId, newLock))
        {
            _logger?.LogInformation("Added new session lock for FileId: {FileId}, SessionId: {SessionId}, LockId: {LockId}", 
                fileId, sessionId, lockId);
            return newLock;
        }
        
        _logger?.LogWarning("Failed to add session lock for FileId: {FileId}, SessionId: {SessionId}", fileId, sessionId);
        return null; // Couldn't add lock (race condition)
    }

    public bool RemoveLock(string fileId)
    {
        if (_fileLocks.TryGetValue(fileId, out var fileSessions))
        {
            // Remove all sessions for this file
            var sessionCount = fileSessions.Count;
            fileSessions.Clear();
            _fileLocks.TryRemove(fileId, out _);
            
            _logger?.LogInformation("Removed all {Count} session(s) for FileId: {FileId}", sessionCount, fileId);
            return true;
        }
        
        _logger?.LogWarning("Failed to remove locks for FileId: {FileId} - no sessions found", fileId);
        return false;
    }

    /// <summary>
    /// Remove a specific session lock for a file
    /// </summary>
    public bool RemoveSessionLock(string fileId, string lockId)
    {
        var sessionId = ExtractSessionId(lockId);
        
        if (_fileLocks.TryGetValue(fileId, out var fileSessions))
        {
            if (fileSessions.TryRemove(sessionId, out _))
            {
                _logger?.LogInformation("Removed session {SessionId} for FileId: {FileId}", sessionId, fileId);
                
                // Clean up empty file entry
                if (fileSessions.IsEmpty)
                {
                    _fileLocks.TryRemove(fileId, out _);
                }
                return true;
            }
        }
        
        _logger?.LogWarning("Failed to remove session {SessionId} for FileId: {FileId} - session not found", sessionId, fileId);
        return false;
    }

    /// <summary>
    /// extend an existing lock on fileId expiration time.
    /// </summary>
    /// <param name="fileId">the fileId to refresh the lock for.</param>
    /// <param name="lockId">include to also update the lockId (optional).</param>
    /// <returns>true if success</returns>
    public bool RefreshLock(string fileId, string? lockId = null)
    {
        if (string.IsNullOrEmpty(lockId))
        {
            _logger?.LogWarning("RefreshLock failed for FileId: {FileId}. No lockId provided", fileId);
            return false;
        }

        var sessionId = ExtractSessionId(lockId);
        
        if (_fileLocks.TryGetValue(fileId, out var fileSessions))
        {
            if (fileSessions.TryGetValue(sessionId, out var existingLock))
            {
                // 1. Check if the current lock has expired.
                if (existingLock.Expired)
                {
                    _logger?.LogWarning("RefreshLock failed for FileId: {FileId}, SessionId: {SessionId}. Lock has expired", fileId, sessionId);
                    // Remove expired session
                    fileSessions.TryRemove(sessionId, out _);
                    return false;
                }

                // 2. Create a new WopiLockInfo with updated DateCreated and LockId
                var refreshedLock = existingLock with 
                { 
                    DateCreated = DateTimeOffset.UtcNow,
                    LockId = lockId // Update with new lockId
                };

                // 3. Update lock in session dictionary
                if (fileSessions.TryUpdate(sessionId, refreshedLock, existingLock))
                {
                    _logger?.LogInformation("Lock refreshed for FileId: {FileId}, SessionId: {SessionId}. New DateCreated: {DateCreated}, LockId: {LockId}", 
                        fileId, sessionId, refreshedLock.DateCreated, refreshedLock.LockId);
                    return true;
                }
                else
                {
                    _logger?.LogWarning("RefreshLock failed for FileId: {FileId}, SessionId: {SessionId}. Concurrent update issue", fileId, sessionId);
                    return false;
                }
            }
            else
            {
                _logger?.LogWarning("RefreshLock failed for FileId: {FileId}, SessionId: {SessionId}. Session not found", fileId, sessionId);
                return false;
            }
        }

        _logger?.LogWarning("RefreshLock failed for FileId: {FileId}, SessionId: {SessionId}. No file sessions found", fileId, sessionId);
        return false; // No lock found to refresh
    }

    /// <summary>
    /// Cleanup stale sessions for a specific user across all files.
    /// This is useful when WebView2 is disposed and recreated.
    /// </summary>
    public int CleanupStaleSessionsForUser(string userId, TimeSpan? maxAge = null)
    {
        var cutoffTime = DateTimeOffset.UtcNow; // Remove ALL old sessions regardless of age
        var removedCount = 0;
        var totalSessions = 0;
        var userSessions = 0;

        _logger?.LogInformation("=== CLEANUP DEBUG START === UserId: {UserId}", userId);

        // Count total sessions first
        foreach (var fileEntry in _fileLocks)
        {
            totalSessions += fileEntry.Value.Count;
        }

        _logger?.LogInformation("TOTAL SESSIONS in memory: {TotalSessions}", totalSessions);
        _logger?.LogInformation("SESSION-TO-USER mapping count: {MappingCount}", _sessionToUser.Count);

        // Debug: Print all sessions and their user mappings
        foreach (var fileEntry in _fileLocks)
        {
            var fileId = fileEntry.Key;
            var fileSessions = fileEntry.Value;
            
            _logger?.LogInformation("File {FileId} has {SessionCount} sessions:", fileId, fileSessions.Count);
            
            foreach (var session in fileSessions)
            {
                var sessionId = session.Key;
                var lockInfo = session.Value;
                
                // Check session-to-user mapping first, then try to extract from lockId
                var sessionUserId = _sessionToUser.TryGetValue(sessionId, out var mappedUserId) 
                    ? mappedUserId 
                    : ExtractUserId(lockInfo.LockId);
                
                _logger?.LogInformation("  Session {SessionId}: UserId={SessionUserId}, LockId={LockId}, Created={Created}", 
                    sessionId, sessionUserId, lockInfo.LockId, lockInfo.DateCreated);
                
                if (string.Equals(sessionUserId, userId, StringComparison.OrdinalIgnoreCase))
                {
                    userSessions++;
                }
            }
        }

        _logger?.LogInformation("Found {UserSessions} sessions belonging to UserId: {UserId}", userSessions, userId);

        // Now perform cleanup - remove ALL sessions for the user regardless of age
        foreach (var fileEntry in _fileLocks)
        {
            var fileId = fileEntry.Key;
            var fileSessions = fileEntry.Value;
            
            var staleSessions = fileSessions.Where(kvp => 
            {
                var sessionId = kvp.Key;
                var lockInfo = kvp.Value;
                
                // Check session-to-user mapping first, then try to extract from lockId
                var sessionUserId = _sessionToUser.TryGetValue(sessionId, out var mappedUserId) 
                    ? mappedUserId 
                    : ExtractUserId(lockInfo.LockId);
                
                // Remove ALL sessions for this user, regardless of age
                return string.Equals(sessionUserId, userId, StringComparison.OrdinalIgnoreCase);
            }).ToList();

            foreach (var staleSession in staleSessions)
            {
                if (fileSessions.TryRemove(staleSession.Key, out var removedLock))
                {
                    // Also remove from session-to-user mapping
                    _sessionToUser.TryRemove(staleSession.Key, out _);
                    
                    removedCount++;
                    _logger?.LogInformation("REMOVED session {SessionId} for UserId: {UserId}, FileId: {FileId}, Age: {Age}", 
                        staleSession.Key, userId, fileId, DateTimeOffset.UtcNow - removedLock.DateCreated);
                }
            }

            // Clean up empty file entries
            if (fileSessions.IsEmpty)
            {
                _fileLocks.TryRemove(fileId, out _);
                _logger?.LogInformation("REMOVED empty file entry for FileId: {FileId}", fileId);
            }
        }

        _logger?.LogInformation("=== CLEANUP RESULT === UserId: {UserId}, Removed: {Count} sessions out of {UserSessions} found", userId, removedCount, userSessions);
        _logger?.LogInformation("=== CLEANUP DEBUG END ===");
        
        return removedCount;
    }

    /// <summary>
    /// Get diagnostic information about all active sessions
    /// </summary>
    public Dictionary<string, List<object>> GetSessionDiagnostics()
    {
        var diagnostics = new Dictionary<string, List<object>>();

        foreach (var fileEntry in _fileLocks)
        {
            var fileId = fileEntry.Key;
            var fileSessions = fileEntry.Value;
            
            var sessionInfo = fileSessions.Values.Select(lockInfo => 
            {
                var sessionId = ExtractSessionId(lockInfo.LockId);
                // Get userId from session-to-user mapping instead of trying to extract from lockId
                var userId = _sessionToUser.TryGetValue(sessionId, out var mappedUserId) ? mappedUserId : "unknown";
                
                return new
                {
                    SessionId = sessionId,
                    UserId = userId,
                    LockId = lockInfo.LockId,
                    DateCreated = lockInfo.DateCreated,
                    Age = DateTimeOffset.UtcNow - lockInfo.DateCreated,
                    Expired = lockInfo.Expired
                };
            }).Cast<object>().ToList();

            if (sessionInfo.Any())
            {
                diagnostics[fileId] = sessionInfo;
            }
        }

        return diagnostics;
    }
}