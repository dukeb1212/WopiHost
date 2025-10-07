# Test script for session cleanup API
$baseUrl = "http://localhost:5000"
$userId = "test-user-123"

Write-Host "=== Testing Session Cleanup API ===" -ForegroundColor Green

# Test 1: Get diagnostics before any operations
Write-Host "`n1. Getting initial diagnostics..." -ForegroundColor Yellow
try {
    $diagnostics = Invoke-RestMethod -Uri "$baseUrl/api/sessioncleanup/diagnostics" -Method Get
    Write-Host "Total files: $($diagnostics.totalFiles)" -ForegroundColor Cyan
    Write-Host "Total sessions: $($diagnostics.totalSessions)" -ForegroundColor Cyan
    $diagnostics.files | ConvertTo-Json -Depth 3
} catch {
    Write-Host "Error getting diagnostics: $_" -ForegroundColor Red
}

# Test 2: Try cleanup for specific user
Write-Host "`n2. Calling cleanup API for user: $userId" -ForegroundColor Yellow
try {
    $cleanup = Invoke-RestMethod -Uri "$baseUrl/api/sessioncleanup/cleanup-user/$userId" -Method Post
    Write-Host "Cleanup result:" -ForegroundColor Cyan
    $cleanup | ConvertTo-Json -Depth 2
} catch {
    Write-Host "Error calling cleanup API: $_" -ForegroundColor Red
}

# Test 3: Get diagnostics after cleanup
Write-Host "`n3. Getting diagnostics after cleanup..." -ForegroundColor Yellow
try {
    $diagnosticsAfter = Invoke-RestMethod -Uri "$baseUrl/api/sessioncleanup/diagnostics" -Method Get
    Write-Host "Total files after: $($diagnosticsAfter.totalFiles)" -ForegroundColor Cyan
    Write-Host "Total sessions after: $($diagnosticsAfter.totalSessions)" -ForegroundColor Cyan
} catch {
    Write-Host "Error getting diagnostics after cleanup: $_" -ForegroundColor Red
}

Write-Host "`n=== Test completed ===" -ForegroundColor Green
