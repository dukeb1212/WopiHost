# Script to simulate WOPI sessions for testing cleanup
$baseUrl = "http://localhost:5000"

Write-Host "=== Simulating WOPI Sessions for Testing ===" -ForegroundColor Green

# Test data - simulate Office Online Server requests
$testData = @(
    @{
        fileId = "test-file-1.docx"
        lockId = '{"S":"session-001","F":4}'
        user = "test-user-123"
    },
    @{
        fileId = "test-file-2.docx"  
        lockId = '{"S":"session-002","F":4}'
        user = "test-user-123"
    },
    @{
        fileId = "test-file-3.docx"
        lockId = '{"S":"session-003","F":4}'
        user = "different-user-456"
    }
)

Write-Host "`nSimulating WOPI Lock requests..." -ForegroundColor Yellow

# Note: Since we don't have direct access to the lock creation endpoints from the WOPI protocol,
# we'll create a simple test endpoint or use a PowerShell HTTP client to simulate the lock creation
# For now, let's check what endpoints are available

try {
    Write-Host "Checking available endpoints..." -ForegroundColor Cyan
    
    # Check if there's a test endpoint we can use
    $response = Invoke-WebRequest -Uri "$baseUrl/api/sessioncleanup/diagnostics" -Method Get
    Write-Host "Diagnostics endpoint works - Status: $($response.StatusCode)" -ForegroundColor Green
    
    # We need to find a way to create sessions. Let's check the WOPI endpoints
    Write-Host "`nNote: To properly test session cleanup, we need to:" -ForegroundColor Yellow
    Write-Host "1. Open actual Office documents through WOPI protocol" -ForegroundColor White
    Write-Host "2. Or create a test endpoint that can simulate lock creation" -ForegroundColor White
    Write-Host "3. Or manually trigger WOPI operations through WebView2" -ForegroundColor White
    
} catch {
    Write-Host "Error: $_" -ForegroundColor Red
}

Write-Host "`n=== Current Session Status ===" -ForegroundColor Green
try {
    $diagnostics = Invoke-RestMethod -Uri "$baseUrl/api/sessioncleanup/diagnostics" -Method Get
    Write-Host "Total files: $($diagnostics.totalFiles)" -ForegroundColor Cyan
    Write-Host "Total sessions: $($diagnostics.totalSessions)" -ForegroundColor Cyan
} catch {
    Write-Host "Error getting diagnostics: $_" -ForegroundColor Red
}
