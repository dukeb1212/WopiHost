# Enhanced test script for session cleanup with fake sessions
$baseUrl = "http://localhost:5000"

Write-Host "=== Enhanced Session Cleanup Test ===" -ForegroundColor Green

# Step 1: Create fake sessions for testing
Write-Host "`n1. Creating fake sessions..." -ForegroundColor Yellow

$fakeSessionsData = @{
    sessions = @(
        @{
            fileId = "test-file-1.docx"
            lockId = '{"S":"session-001","F":4}'
        },
        @{
            fileId = "test-file-2.docx"  
            lockId = '{"S":"session-002","F":4}'
        },
        @{
            fileId = "test-file-1.docx"
            lockId = '{"S":"session-003","F":4}'
        }
    )
} 

$body = $fakeSessionsData | ConvertTo-Json -Depth 3
Write-Host "Request body: $body" -ForegroundColor Gray

try {
    $createResult = Invoke-RestMethod -Uri "$baseUrl/api/sessioncleanup/test/create-fake-sessions" -Method Post -Body $body -ContentType "application/json"
    Write-Host "Fake sessions created successfully:" -ForegroundColor Green
    $createResult | ConvertTo-Json -Depth 3
} catch {
    Write-Host "Error creating fake sessions: $_" -ForegroundColor Red
    Write-Host "Response: $($_.Exception.Response | Out-String)" -ForegroundColor Red
}

# Step 2: Get diagnostics to see current sessions
Write-Host "`n2. Getting current session diagnostics..." -ForegroundColor Yellow
try {
    $diagnostics = Invoke-RestMethod -Uri "$baseUrl/api/sessioncleanup/diagnostics" -Method Get
    Write-Host "Current session state:" -ForegroundColor Cyan
    Write-Host "Total files: $($diagnostics.totalFiles)" -ForegroundColor Cyan
    Write-Host "Total sessions: $($diagnostics.totalSessions)" -ForegroundColor Cyan
    $diagnostics.files | ConvertTo-Json -Depth 3
} catch {
    Write-Host "Error getting diagnostics: $_" -ForegroundColor Red
}

# Step 3: Test cleanup for a specific user
Write-Host "`n3. Testing cleanup for specific user..." -ForegroundColor Yellow
$testUserId = "test-user-123"

try {
    $cleanup = Invoke-RestMethod -Uri "$baseUrl/api/sessioncleanup/cleanup-user/$testUserId" -Method Post
    Write-Host "Cleanup result:" -ForegroundColor Cyan
    $cleanup | ConvertTo-Json -Depth 2
} catch {
    Write-Host "Error calling cleanup API: $_" -ForegroundColor Red
}

# Step 4: Get diagnostics after cleanup
Write-Host "`n4. Getting diagnostics after cleanup..." -ForegroundColor Yellow
try {
    $diagnosticsAfter = Invoke-RestMethod -Uri "$baseUrl/api/sessioncleanup/diagnostics" -Method Get
    Write-Host "Session state after cleanup:" -ForegroundColor Cyan
    Write-Host "Total files after: $($diagnosticsAfter.totalFiles)" -ForegroundColor Cyan
    Write-Host "Total sessions after: $($diagnosticsAfter.totalSessions)" -ForegroundColor Cyan
    $diagnosticsAfter.files | ConvertTo-Json -Depth 3
} catch {
    Write-Host "Error getting diagnostics after cleanup: $_" -ForegroundColor Red
}

Write-Host "`n=== Enhanced test completed ===" -ForegroundColor Green
