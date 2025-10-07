# Demo script for Session Cleanup APIs - WebView2 Integration
# This demonstrates the difference between unsafe and safe cleanup methods

$baseUrl = "http://localhost:5000"
$userId = "test-user-123"

Write-Host "=== WOPI Session Cleanup Demo ===" -ForegroundColor Green
Write-Host "Demo: Safe vs Unsafe session cleanup for WebView2 disposal" -ForegroundColor Gray
Write-Host ""

# Function to show current session state
function Show-SessionState {
    param($title)
    Write-Host "`n$title" -ForegroundColor Cyan
    try {
        $diagnostics = Invoke-RestMethod -Uri "$baseUrl/api/sessioncleanup/diagnostics" -Method Get
        Write-Host "Total active sessions: $($diagnostics.totalSessions)" -ForegroundColor White
        if ($diagnostics.totalSessions -gt 0) {
            foreach ($file in $diagnostics.sessionsByFile.Keys) {
                Write-Host "  - $file" -ForegroundColor Gray
            }
        }
    } catch {
        Write-Host "Error getting diagnostics: $_" -ForegroundColor Red
    }
}

# Reset environment
Write-Host "1. Preparing test environment..." -ForegroundColor Yellow
$clearResult = Invoke-RestMethod -Uri "$baseUrl/api/sessioncleanup/test/clear-all-sessions" -Method Post
Write-Host "Cleared $($clearResult.sessionsBefore) existing sessions" -ForegroundColor Green

# Create test sessions
Write-Host "`n2. Creating test sessions..." -ForegroundColor Yellow
$fakeSessionsData = @{
    sessions = @(
        @{
            fileId = "document1.docx"
            lockId = '{"S":"session-doc1","F":4}'
            userId = $userId
        },
        @{
            fileId = "document2.pptx"  
            lockId = '{"S":"session-doc2","F":4}'
            userId = $userId
        }
    )
} 

$body = $fakeSessionsData | ConvertTo-Json -Depth 3
$createResult = Invoke-RestMethod -Uri "$baseUrl/api/sessioncleanup/test/create-fake-sessions" -Method Post -Body $body -ContentType "application/json"
Write-Host "Created $($createResult.totalSessions) test sessions for user: $userId" -ForegroundColor Green

Show-SessionState "Initial State:"

# Demo 1: UNSAFE cleanup (immediate risk)
Write-Host "`n=== DEMO 1: UNSAFE CLEANUP ===" -ForegroundColor Red
Write-Host "Scenario: WebView2 disposed immediately without waiting for saves" -ForegroundColor Gray
Write-Host "Risk: User changes may be lost if Office Online Server hasn't completed saving" -ForegroundColor Red

# Recreate sessions for demo
$createResult2 = Invoke-RestMethod -Uri "$baseUrl/api/sessioncleanup/test/create-fake-sessions" -Method Post -Body $body -ContentType "application/json"

Write-Host "`nSimulating: User has unsaved changes..." -ForegroundColor Yellow
Write-Host "Calling UNSAFE cleanup API..." -ForegroundColor Red

$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
$unsafeCleanup = Invoke-RestMethod -Uri "$baseUrl/api/sessioncleanup/cleanup-user/$userId" -Method Post
$stopwatch.Stop()

Write-Host "✗ UNSAFE Result: Cleaned $($unsafeCleanup.cleanedSessionsCount) sessions in $($stopwatch.ElapsedMilliseconds)ms" -ForegroundColor Red
Write-Host "  WARNING: Data loss risk - cleanup happened immediately!" -ForegroundColor Red

Show-SessionState "After Unsafe Cleanup:"

# Demo 2: SAFE cleanup (with save protection)
Write-Host "`n=== DEMO 2: SAFE CLEANUP ===" -ForegroundColor Green  
Write-Host "Scenario: WebView2 disposed with save delay protection" -ForegroundColor Gray
Write-Host "Benefit: Waits for Office Online Server to complete saves before cleanup" -ForegroundColor Green

# Recreate sessions for demo
$createResult3 = Invoke-RestMethod -Uri "$baseUrl/api/sessioncleanup/test/create-fake-sessions" -Method Post -Body $body -ContentType "application/json"

Show-SessionState "Before Safe Cleanup:"

Write-Host "`nSimulating: User has unsaved changes..." -ForegroundColor Yellow
Write-Host "Calling SAFE cleanup API with save delay..." -ForegroundColor Green

$saveDelay = 3 # 3 seconds for demo speed
$safeCleanupUrl = "$baseUrl/api/sessioncleanup/safe-cleanup-user/$userId" + "?saveDelaySeconds=$saveDelay"

$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
$safeCleanup = Invoke-RestMethod -Uri $safeCleanupUrl -Method Post
$stopwatch.Stop()

Write-Host "✓ SAFE Result: Cleaned $($safeCleanup.cleanedSessionsCount) sessions in $($stopwatch.ElapsedMilliseconds)ms" -ForegroundColor Green
Write-Host "  SUCCESS: Save delay protected user data!" -ForegroundColor Green

Show-SessionState "After Safe Cleanup:"

# Summary and recommendations
Write-Host "`n=== SUMMARY AND RECOMMENDATIONS ===" -ForegroundColor Green
Write-Host ""
Write-Host "API Endpoints:" -ForegroundColor Yellow
Write-Host "• Unsafe: POST /api/sessioncleanup/cleanup-user/{userId}" -ForegroundColor Red
Write-Host "  - Immediate cleanup" -ForegroundColor Red
Write-Host "  - Risk of data loss" -ForegroundColor Red
Write-Host ""
Write-Host "• Safe:   POST /api/sessioncleanup/safe-cleanup-user/{userId}?saveDelaySeconds={delay}" -ForegroundColor Green
Write-Host "  - Waits for saves to complete" -ForegroundColor Green
Write-Host "  - Protects user data" -ForegroundColor Green
Write-Host ""
Write-Host "For WebView2 disposal:" -ForegroundColor Yellow
Write-Host "✓ RECOMMENDED: Use safe-cleanup-user endpoint" -ForegroundColor Green
Write-Host "✓ RECOMMENDED: Set saveDelaySeconds=3-5 for typical Office documents" -ForegroundColor Green
Write-Host "✓ RECOMMENDED: Monitor for save completion before disposal" -ForegroundColor Green
Write-Host ""
Write-Host "Example C# usage:" -ForegroundColor Cyan
Write-Host 'await httpClient.PostAsync($"api/sessioncleanup/safe-cleanup-user/{userId}?saveDelaySeconds=5");' -ForegroundColor Gray

Write-Host "`n=== Demo Complete ===" -ForegroundColor Green
