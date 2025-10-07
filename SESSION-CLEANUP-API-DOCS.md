# WOPI Session Cleanup API Documentation

## Overview
Session cleanup APIs for WebView2 disposal scenarios where user sessions need to be safely terminated while protecting data integrity.

## Problem Statement
When disposing WebView2 controls that host Office Online Server documents, immediate session cleanup can cause data loss if the user has unsaved changes. Office Online Server needs time to complete auto-save operations before sessions are terminated.

## Solution
Two cleanup approaches are provided:

### 1. Unsafe Cleanup (Immediate)
- **Endpoint**: `POST /api/sessioncleanup/cleanup-user/{userId}`
- **Behavior**: Immediate session termination
- **Risk**: Data loss if saves are in progress
- **Use Case**: When you're certain no unsaved changes exist

### 2. Safe Cleanup (With Save Delay)
- **Endpoint**: `POST /api/sessioncleanup/safe-cleanup-user/{userId}?saveDelaySeconds={delay}`
- **Behavior**: Waits for specified delay before cleanup
- **Benefit**: Allows Office Online Server to complete saves
- **Use Case**: Normal WebView2 disposal scenarios

## API Reference

### Safe Cleanup Endpoint (Recommended)

```http
POST /api/sessioncleanup/safe-cleanup-user/{userId}?saveDelaySeconds={delay}
```

**Parameters:**
- `userId` (string): User identifier to cleanup sessions for
- `saveDelaySeconds` (int, optional): Delay in seconds to wait for saves (default: 3)

**Response:**
```json
{
  "userId": "test-user-123",
  "cleanedSessionsCount": 2,
  "totalSessionsBefore": 2,
  "totalSessionsAfter": 0,
  "filesAffected": 2,
  "delayApplied": "3s",
  "timestamp": "2025-01-08T19:39:11+00:00"
}
```

### Unsafe Cleanup Endpoint

```http
POST /api/sessioncleanup/cleanup-user/{userId}
```

**Parameters:**
- `userId` (string): User identifier to cleanup sessions for
- `maxAgeMinutes` (int, optional): Only clean sessions older than this (default: 2)
- `waitForSave` (bool, optional): Enable save waiting (default: true)
- `saveTimeoutSeconds` (int, optional): Save wait timeout (default: 10)

**Response:**
```json
{
  "userId": "test-user-123",
  "cleanedSessionsCount": 2,
  "totalSessionsBefore": 2,
  "totalSessionsAfter": 0,
  "filesAffected": 2,
  "waitForSave": true,
  "saveTimeoutSeconds": 10,
  "timestamp": "2025-01-08T19:36:48+00:00"
}
```

## Implementation Example (C#)

### Safe Cleanup for WebView2 Disposal

```csharp
public async Task DisposeWebView2Safely(string userId)
{
    try
    {
        // Recommended: Use safe cleanup with delay
        var response = await httpClient.PostAsync(
            $"api/sessioncleanup/safe-cleanup-user/{userId}?saveDelaySeconds=5");
        
        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<CleanupResult>();
            Console.WriteLine($"Safely cleaned {result.CleanedSessionsCount} sessions");
        }
    }
    catch (Exception ex)
    {
        // Handle cleanup failure
        Console.WriteLine($"Cleanup failed: {ex.Message}");
    }
    finally
    {
        // Proceed with WebView2 disposal
        webView2Control?.Dispose();
    }
}
```

### Advanced Cleanup with Progress Monitoring

```csharp
public async Task DisposeWebView2WithMonitoring(string userId)
{
    // Show user that cleanup is in progress
    ShowCleanupProgress("Saving changes...");
    
    try
    {
        // Use safe cleanup with appropriate delay for document type
        int delay = GetRecommendedDelay(); // 3-5 seconds typical
        
        var response = await httpClient.PostAsync(
            $"api/sessioncleanup/safe-cleanup-user/{userId}?saveDelaySeconds={delay}");
        
        var result = await response.Content.ReadFromJsonAsync<CleanupResult>();
        
        if (result.CleanedSessionsCount > 0)
        {
            ShowCleanupProgress($"Saved and cleaned {result.CleanedSessionsCount} documents");
        }
    }
    catch (Exception ex)
    {
        ShowError($"Failed to save changes: {ex.Message}");
        
        // Optionally ask user if they want to force cleanup
        if (await ConfirmForceCleanup())
        {
            await httpClient.PostAsync($"api/sessioncleanup/cleanup-user/{userId}");
        }
    }
    finally
    {
        HideCleanupProgress();
        webView2Control?.Dispose();
    }
}

private int GetRecommendedDelay()
{
    // Adjust based on document types and typical save times
    return 5; // 5 seconds for most Office documents
}
```

## Testing

Use the provided PowerShell scripts to test the APIs:

```powershell
# Run demo comparing unsafe vs safe cleanup
.\demo-session-cleanup-simple.ps1

# Test safe cleanup specifically  
.\test-safe-cleanup-fixed.ps1
```

## Recommendations

### For Production Use:
1. **Always use safe cleanup** (`safe-cleanup-user`) for WebView2 disposal
2. **Set appropriate delay**: 3-5 seconds for typical Office documents
3. **Monitor cleanup results** to ensure sessions were properly cleaned
4. **Handle failures gracefully** with fallback to unsafe cleanup if needed

### Delay Guidelines:
- **Word documents**: 3-5 seconds
- **Excel spreadsheets**: 5-7 seconds (more complex saves)
- **PowerPoint presentations**: 3-5 seconds
- **Large files (>10MB)**: 7-10 seconds
- **Multiple documents**: Add 1-2 seconds per additional document

### Error Handling:
- Always implement timeout handling for cleanup calls
- Provide user feedback during save delays
- Log cleanup results for debugging
- Have fallback strategy for cleanup failures

## Diagnostics

### Session Diagnostics Endpoint

```http
GET /api/sessioncleanup/diagnostics
```

Returns current session state for debugging:

```json
{
  "totalSessions": 2,
  "filesWithSessions": 2,
  "sessionsByFile": {
    "document1.docx": [
      {
        "sessionId": "session-doc1",
        "userId": "test-user-123",
        "lockId": "{\"S\":\"session-doc1\",\"F\":4}",
        "created": "2025-01-08T19:39:06+00:00"
      }
    ]
  },
  "timestamp": "2025-01-08T19:39:11+00:00"
}
```

## Security Considerations

1. **User Authorization**: Ensure cleanup requests are authorized for the specified userId
2. **Rate Limiting**: Implement rate limiting to prevent cleanup abuse
3. **Audit Logging**: Log all cleanup operations for security auditing
4. **Input Validation**: Validate userId format and delay parameters

## Troubleshooting

### Common Issues:
1. **Sessions not found**: Check userId extraction from JWT tokens
2. **Cleanup timeout**: Increase saveDelaySeconds parameter
3. **Unauthorized access**: Verify JWT token validity and user permissions
4. **Performance issues**: Monitor cleanup timing and optimize delay parameters

### Debug Logging:
Enable detailed logging to troubleshoot cleanup issues:
- Session creation and mapping
- User ID extraction from JWT
- Cleanup operation details
- Save wait timing information
