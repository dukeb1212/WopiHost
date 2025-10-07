# Test script for safe session cleanup with save delay
$baseUrl = "http://localhost:5000"
$userId = "test-user-123"

Write-Host "=== Testing Safe Session Cleanup with Save Delay ===" -ForegroundColor Green

# Step 1: Clear any existing sessions
Write-Host "`n1. Clearing existing sessions..." -ForegroundColor Yellow
try {
    $clearResult = Invoke-RestMethod -Uri "$baseUrl/api/sessioncleanup/test/clear-all-sessions" -Method Post
    Write-Host "Clear result: Cleared $($clearResult.sessionsBefore) sessions" -ForegroundColor Green
} catch {
    Write-Host "Error clearing sessions: $_" -ForegroundColor Red
}

# Step 2: Create fake sessions for testing
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

try {
    $createResult = Invoke-RestMethod -Uri "$baseUrl/api/sessioncleanup/test/create-fake-sessions" -Method Post -Body $body -ContentType "application/json"
    Write-Host "Created $($createResult.totalSessions) sessions successfully" -ForegroundColor Green
} catch {
    Write-Host "Error creating fake sessions: $_" -ForegroundColor Red
}

# Step 3: Simulate user work (Office is "saving")
Write-Host "`n3. Simulating user work - Office Online Server is saving..." -ForegroundColor Yellow
Write-Host "   (In real scenario, user would be editing documents)" -ForegroundColor Gray
Start-Sleep -Seconds 2

# Step 4: Test UNSAFE cleanup (immediate)
Write-Host "`n4. Testing UNSAFE cleanup (immediate - might lose changes)..." -ForegroundColor Red

# Recreate sessions for test
try {
    $createResult2 = Invoke-RestMethod -Uri "$baseUrl/api/sessioncleanup/test/create-fake-sessions" -Method Post -Body $body -ContentType "application/json"
    Write-Host "Recreated $($createResult2.totalSessions) sessions" -ForegroundColor Green
    
    # Immediate cleanup
    $unsafeCleanup = Invoke-RestMethod -Uri "$baseUrl/api/sessioncleanup/cleanup-user/$userId" -Method Post
    Write-Host "UNSAFE cleanup result: Cleaned $($unsafeCleanup.cleanedSessionsCount) sessions immediately" -ForegroundColor Red
    Write-Host "   Risk: Changes might be lost!" -ForegroundColor Red
    
} catch {
    Write-Host "Error in unsafe cleanup test: $_" -ForegroundColor Red
}

# Step 5: Test SAFE cleanup (with delay)
Write-Host "`n5. Testing SAFE cleanup (with save delay)..." -ForegroundColor Green

# Recreate sessions for test
try {
    $createResult3 = Invoke-RestMethod -Uri "$baseUrl/api/sessioncleanup/test/create-fake-sessions" -Method Post -Body $body -ContentType "application/json"
    Write-Host "Recreated $($createResult3.totalSessions) sessions" -ForegroundColor Green
    
    # Safe cleanup with delay
    $saveDelay = 5 # 5 seconds delay
    Write-Host "Calling safe cleanup with ${saveDelay}s delay..." -ForegroundColor Yellow
    
    $safeCleanup = Invoke-RestMethod -Uri "$baseUrl/api/sessioncleanup/safe-cleanup-user/$userId?saveDelaySeconds=$saveDelay" -Method Post
    Write-Host "SAFE cleanup result: Cleaned $($safeCleanup.cleanedSessionsCount) sessions after ${saveDelay}s delay" -ForegroundColor Green
    Write-Host "   Benefit: Saves completed before cleanup!" -ForegroundColor Green
    
} catch {
    Write-Host "Error in safe cleanup test: $_" -ForegroundColor Red
}

# Step 6: Final diagnostics
Write-Host "`n6. Final session state..." -ForegroundColor Yellow
try {
    $finalDiagnostics = Invoke-RestMethod -Uri "$baseUrl/api/sessioncleanup/diagnostics" -Method Get
    Write-Host "Final sessions: $($finalDiagnostics.totalSessions)" -ForegroundColor Cyan
} catch {
    Write-Host "Error getting final diagnostics: $_" -ForegroundColor Red
}

Write-Host "`n=== Test Summary ===" -ForegroundColor Green
Write-Host "- UNSAFE cleanup: Immediate, risk of data loss" -ForegroundColor Red
Write-Host "- SAFE cleanup: Waits for saves, preserves data" -ForegroundColor Green
Write-Host "`nRecommendation: Use safe-cleanup-user endpoint for WebView2 disposal" -ForegroundColor Yellow
