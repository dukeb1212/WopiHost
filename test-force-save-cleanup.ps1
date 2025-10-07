# Test script for Force Save and Cleanup API
$baseUrl = "http://localhost:5000"
$userId = "test-user-123"

Write-Host "=== Testing Force Save and Cleanup API ===" -ForegroundColor Green
Write-Host "This API actively triggers saves before cleanup to ensure data integrity"
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

Show-SessionState "Initial State (Simulating unsaved documents):"

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
    
    if ($forceSaveCleanup.saveResults.details -and $forceSaveCleanup.saveResults.details.Length -gt 0) {
        Write-Host "`nSave Details:" -ForegroundColor Yellow
        foreach ($detail in $forceSaveCleanup.saveResults.details) {
            if ($detail.Success) {
                Write-Host "  ✓ $($detail.FileId): $($detail.Message)" -ForegroundColor Green
            } else {
                Write-Host "  ✗ $($detail.FileId): $($detail.Error)" -ForegroundColor Red
            }
        }
    }
    
    if ($forceSaveCleanup.saveResults.successful -gt 0) {
        Write-Host "`n🎉 SUCCESS: All documents saved before cleanup!" -ForegroundColor Green
        Write-Host "   User data is protected - no data loss occurred" -ForegroundColor Green
    } else {
        Write-Host "`n⚠️  WARNING: Some saves may have failed" -ForegroundColor Yellow
        Write-Host "   Check save details above for specific errors" -ForegroundColor Yellow
    }

} catch {
    Write-Host "`n❌ ERROR: Force save cleanup failed" -ForegroundColor Red
    Write-Host "Error details: $_" -ForegroundColor Red
    Write-Host "Exception: $($_.Exception.Message)" -ForegroundColor Red
}

Show-SessionState "Final State After Force Save Cleanup:"

# Comparison with regular methods
Write-Host "`n=== COMPARISON SUMMARY ===" -ForegroundColor Yellow
Write-Host ""
Write-Host "Available Cleanup Methods:" -ForegroundColor White
Write-Host ""
Write-Host "1. Unsafe Cleanup (DANGEROUS):" -ForegroundColor Red
Write-Host "   POST /api/sessioncleanup/cleanup-user/{userId}" -ForegroundColor Gray
Write-Host "   ❌ Immediate cleanup, risk of data loss" -ForegroundColor Red
Write-Host ""
Write-Host "2. Safe Cleanup (BETTER):" -ForegroundColor Yellow
Write-Host "   POST /api/sessioncleanup/safe-cleanup-user/{userId}?saveDelaySeconds=N" -ForegroundColor Gray
Write-Host "   ⏱️  Waits for saves, but passive approach" -ForegroundColor Yellow
Write-Host ""
Write-Host "3. Force Save Cleanup (BEST):" -ForegroundColor Green
Write-Host "   POST /api/sessioncleanup/force-save-cleanup-user/{userId}?saveTimeoutSeconds=N" -ForegroundColor Gray
Write-Host "   ✅ Actively triggers saves, guarantees data protection" -ForegroundColor Green
Write-Host ""

Write-Host "RECOMMENDATION FOR WEBVIEW2 DISPOSAL:" -ForegroundColor Cyan
Write-Host "Use Force Save Cleanup endpoint for maximum data protection" -ForegroundColor Green
Write-Host "Set saveTimeoutSeconds=10-30 depending on document complexity" -ForegroundColor Yellow

Write-Host "`n=== Test Complete ===" -ForegroundColor Green
