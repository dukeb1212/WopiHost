# Simple test script for safe session cleanup 
$baseUrl = "http://localhost:5000"
$userId = "test-user-123"
$saveDelay = 5

Write-Host "=== Testing Safe Session Cleanup (Fixed) ===" -ForegroundColor Green

# Clear existing sessions
Write-Host "`n1. Clearing existing sessions..." -ForegroundColor Yellow
$clearResult = Invoke-RestMethod -Uri "$baseUrl/api/sessioncleanup/test/clear-all-sessions" -Method Post
Write-Host "Cleared: $($clearResult.sessionsBefore) sessions" -ForegroundColor Green

# Create fake sessions 
Write-Host "`n2. Creating fake sessions..." -ForegroundColor Yellow
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
Write-Host "Created: $($createResult.totalSessions) sessions" -ForegroundColor Green

# Test safe cleanup with explicit URL construction
Write-Host "`n3. Testing safe cleanup with explicit URL..." -ForegroundColor Yellow
Write-Host "UserId: '$userId'" -ForegroundColor Gray
Write-Host "SaveDelay: $saveDelay seconds" -ForegroundColor Gray

$safeCleanupUrl = "$baseUrl/api/sessioncleanup/safe-cleanup-user/$userId" + "?saveDelaySeconds=$saveDelay"
Write-Host "URL: $safeCleanupUrl" -ForegroundColor Gray

try {
    $safeCleanup = Invoke-RestMethod -Uri $safeCleanupUrl -Method Post
    Write-Host "SAFE cleanup result: Cleaned $($safeCleanup.cleanedSessionsCount) sessions" -ForegroundColor Green
} catch {
    Write-Host "Error in safe cleanup: $_" -ForegroundColor Red
    Write-Host "Exception details: $($_.Exception.Message)" -ForegroundColor Red
}

# Final diagnostics
Write-Host "`n4. Final diagnostics..." -ForegroundColor Yellow
$finalDiagnostics = Invoke-RestMethod -Uri "$baseUrl/api/sessioncleanup/diagnostics" -Method Get
Write-Host "Remaining sessions: $($finalDiagnostics.totalSessions)" -ForegroundColor Cyan

Write-Host "`n=== Test Complete ===" -ForegroundColor Green
