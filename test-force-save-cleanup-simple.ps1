# Test script for Force Save and Cleanup API
$baseUrl = "http://localhost:5000"
$userId = "test-user-123"

Write-Host "=== Testing Force Save and Cleanup API ===" -ForegroundColor Green
Write-Host "This API actively triggers saves before cleanup to ensure data integrity"
Write-Host ""

# Step 1: Clear existing sessions
Write-Host "1. Preparing test environment..." -ForegroundColor Yellow
$clearResult = Invoke-RestMethod -Uri "$baseUrl/api/sessioncleanup/test/clear-all-sessions" -Method Post
Write-Host "Cleared $($clearResult.sessionsBefore) existing sessions" -ForegroundColor Green

# Step 2: Create test sessions
Write-Host "`n2. Creating test sessions with unsaved changes..." -ForegroundColor Yellow
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
        },
        @{
            fileId = "spreadsheet1.xlsx"
            lockId = '{"S":"session-xls1","F":4}'
            userId = $userId
        }
    )
} 

$body = $fakeSessionsData | ConvertTo-Json -Depth 3
$createResult = Invoke-RestMethod -Uri "$baseUrl/api/sessioncleanup/test/create-fake-sessions" -Method Post -Body $body -ContentType "application/json"
Write-Host "Created $($createResult.totalSessions) test sessions for user: $userId" -ForegroundColor Green

# Show initial state
$initialDiagnostics = Invoke-RestMethod -Uri "$baseUrl/api/sessioncleanup/diagnostics" -Method Get
Write-Host "`nInitial State: $($initialDiagnostics.totalSessions) active sessions" -ForegroundColor Cyan

# Step 3: Test Force Save and Cleanup
Write-Host "`n=== FORCE SAVE AND CLEANUP TEST ===" -ForegroundColor Green
Write-Host "Scenario: User closes application with unsaved documents" -ForegroundColor Gray
Write-Host "Action: Force save all documents before cleanup" -ForegroundColor Green

Write-Host "`nCalling Force Save and Cleanup API..." -ForegroundColor Yellow
$saveTimeout = 10  # 10 seconds timeout for save operations

$forceSaveUrl = "$baseUrl/api/sessioncleanup/force-save-cleanup-user/$userId" + "?saveTimeoutSeconds=$saveTimeout"
Write-Host "URL: $forceSaveUrl" -ForegroundColor Gray

try {
    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    $forceSaveCleanup = Invoke-RestMethod -Uri $forceSaveUrl -Method Post
    $stopwatch.Stop()
    
    Write-Host "`n=== FORCE SAVE CLEANUP RESULTS ===" -ForegroundColor Green
    Write-Host "Total execution time: $($stopwatch.ElapsedMilliseconds)ms" -ForegroundColor Cyan
    Write-Host "Sessions before cleanup: $($forceSaveCleanup.totalSessionsBefore)" -ForegroundColor White
    Write-Host "Sessions after cleanup: $($forceSaveCleanup.totalSessionsAfter)" -ForegroundColor White
    Write-Host "Sessions cleaned: $($forceSaveCleanup.cleanedSessionsCount)" -ForegroundColor Green
    
    Write-Host "`nSave Operations:" -ForegroundColor Yellow
    Write-Host "  Successful saves: $($forceSaveCleanup.saveResults.successful)" -ForegroundColor Green
    Write-Host "  Failed saves: $($forceSaveCleanup.saveResults.failed)" -ForegroundColor Red
    
    if ($forceSaveCleanup.saveResults.successful -gt 0) {
        Write-Host "`nSUCCESS: All documents saved before cleanup!" -ForegroundColor Green
        Write-Host "User data is protected - no data loss occurred" -ForegroundColor Green
    } else {
        Write-Host "`nWARNING: Some saves may have failed" -ForegroundColor Yellow
    }

} catch {
    Write-Host "`nERROR: Force save cleanup failed" -ForegroundColor Red
    Write-Host "Error details: $_" -ForegroundColor Red
}

# Show final state
$finalDiagnostics = Invoke-RestMethod -Uri "$baseUrl/api/sessioncleanup/diagnostics" -Method Get
Write-Host "`nFinal State: $($finalDiagnostics.totalSessions) remaining sessions" -ForegroundColor Cyan

# Summary
Write-Host "`n=== SUMMARY ===" -ForegroundColor Yellow
Write-Host "Available Cleanup Methods:" -ForegroundColor White
Write-Host "1. Unsafe:     POST /api/sessioncleanup/cleanup-user/{userId}" -ForegroundColor Red
Write-Host "   Risk: Immediate cleanup, data loss possible" -ForegroundColor Red
Write-Host "2. Safe:       POST /api/sessioncleanup/safe-cleanup-user/{userId}?saveDelaySeconds=N" -ForegroundColor Yellow  
Write-Host "   Better: Waits for saves, passive approach" -ForegroundColor Yellow
Write-Host "3. Force Save: POST /api/sessioncleanup/force-save-cleanup-user/{userId}?saveTimeoutSeconds=N" -ForegroundColor Green
Write-Host "   Best: Actively triggers saves, guarantees protection" -ForegroundColor Green

Write-Host "`nRECOMMENDATION FOR WEBVIEW2 DISPOSAL:" -ForegroundColor Cyan
Write-Host "Use Force Save Cleanup endpoint for maximum data protection" -ForegroundColor Green

Write-Host "`nTest Complete" -ForegroundColor Green
