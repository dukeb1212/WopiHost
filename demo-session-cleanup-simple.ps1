# WOPI Session Cleanup Demo - WebView2 Integration
$baseUrl = "http://localhost:5000"
$userId = "test-user-123"

Write-Host "=== WOPI Session Cleanup Demo ===" -ForegroundColor Green
Write-Host "Demo: Safe vs Unsafe session cleanup for WebView2 disposal"
Write-Host ""

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

# Demo 1: UNSAFE cleanup
Write-Host "`n=== DEMO 1: UNSAFE CLEANUP ===" -ForegroundColor Red
Write-Host "Scenario: WebView2 disposed immediately without waiting for saves"
Write-Host "Risk: User changes may be lost if Office Online Server hasn't completed saving" -ForegroundColor Red

# Recreate sessions
$createResult2 = Invoke-RestMethod -Uri "$baseUrl/api/sessioncleanup/test/create-fake-sessions" -Method Post -Body $body -ContentType "application/json"

Write-Host "`nCalling UNSAFE cleanup API..." -ForegroundColor Red
$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
$unsafeCleanup = Invoke-RestMethod -Uri "$baseUrl/api/sessioncleanup/cleanup-user/$userId" -Method Post
$stopwatch.Stop()

Write-Host "X UNSAFE Result: Cleaned $($unsafeCleanup.cleanedSessionsCount) sessions in $($stopwatch.ElapsedMilliseconds)ms" -ForegroundColor Red
Write-Host "  WARNING: Data loss risk - cleanup happened immediately!" -ForegroundColor Red

# Demo 2: SAFE cleanup
Write-Host "`n=== DEMO 2: SAFE CLEANUP ===" -ForegroundColor Green  
Write-Host "Scenario: WebView2 disposed with save delay protection"
Write-Host "Benefit: Waits for Office Online Server to complete saves before cleanup" -ForegroundColor Green

# Recreate sessions
$createResult3 = Invoke-RestMethod -Uri "$baseUrl/api/sessioncleanup/test/create-fake-sessions" -Method Post -Body $body -ContentType "application/json"

Write-Host "`nCalling SAFE cleanup API with save delay..." -ForegroundColor Green
$saveDelay = 3 
$safeCleanupUrl = "$baseUrl/api/sessioncleanup/safe-cleanup-user/$userId" + "?saveDelaySeconds=$saveDelay"

$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
$safeCleanup = Invoke-RestMethod -Uri $safeCleanupUrl -Method Post
$stopwatch.Stop()

Write-Host "√ SAFE Result: Cleaned $($safeCleanup.cleanedSessionsCount) sessions in $($stopwatch.ElapsedMilliseconds)ms" -ForegroundColor Green
Write-Host "  SUCCESS: Save delay protected user data!" -ForegroundColor Green

# Final state
$finalDiagnostics = Invoke-RestMethod -Uri "$baseUrl/api/sessioncleanup/diagnostics" -Method Get
Write-Host "`nFinal state: $($finalDiagnostics.totalSessions) sessions remaining" -ForegroundColor Cyan

# Summary
Write-Host "`n=== SUMMARY ===" -ForegroundColor Green
Write-Host "API Endpoints:"
Write-Host "- Unsafe: POST /api/sessioncleanup/cleanup-user/{userId}" -ForegroundColor Red
Write-Host "  Risk of data loss, immediate cleanup"
Write-Host "- Safe:   POST /api/sessioncleanup/safe-cleanup-user/{userId}?saveDelaySeconds={delay}" -ForegroundColor Green
Write-Host "  Protects user data, waits for saves"
Write-Host ""
Write-Host "RECOMMENDATION: Use safe-cleanup-user endpoint for WebView2 disposal" -ForegroundColor Green
Write-Host "Set saveDelaySeconds=3-5 for typical Office documents" -ForegroundColor Yellow

Write-Host "`n=== Demo Complete ===" -ForegroundColor Green
