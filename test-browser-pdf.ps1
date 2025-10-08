# Test PDF viewer với trình duyệt
Write-Host "Testing PDF Viewer in Browser..." -ForegroundColor Cyan

$baseUrl = "http://localhost:5000"
$apiKey = "v6MQZpQHjh7BCm6j59Jj70oqJTAM+fV1phxgb4VAWwk="
$fileId = 55
$userId = 3

$headers = @{
    "X-API-Key" = $apiKey
}

# Get Action URL
try {
    $response = Invoke-RestMethod -Uri "$baseUrl/api/action/$fileId`?action=view&userId=$userId" -Headers $headers -Method Get -UseBasicParsing -ErrorAction Stop
    $actionUrl = $response.ActionUrl
    
    Write-Host "Action URL: $actionUrl" -ForegroundColor Green
    
    # Test direct parameters
    Write-Host "`nTesting with explicit parameters..." -ForegroundColor Yellow
    $explicitUrl = "$baseUrl/viewers/pdf.html?fileId=55&access_token=$($response.AccessToken)&fileName=test.pdf"
    
    Write-Host "Explicit URL: $explicitUrl" -ForegroundColor White
    
    Write-Host "`nOpening URLs in browser for manual testing..." -ForegroundColor Yellow
    Write-Host "1. Action URL (from API)"
    Write-Host "2. Explicit URL (manual parameters)"
    Write-Host "3. Direct PDF API"
    
    $choice = Read-Host "`nWhich URL to open? (1/2/3)"
    
    switch ($choice) {
        "1" { 
            Write-Host "Opening Action URL..." -ForegroundColor Green
            Start-Process $actionUrl 
        }
        "2" { 
            Write-Host "Opening Explicit URL..." -ForegroundColor Green
            Start-Process $explicitUrl 
        }
        "3" { 
            $pdfApiUrl = "$baseUrl/api/pdf/$fileId`?access_token=$($response.AccessToken)"
            Write-Host "Opening Direct PDF API..." -ForegroundColor Green
            Start-Process $pdfApiUrl 
        }
        default { 
            Write-Host "Opening Action URL by default..." -ForegroundColor Green
            Start-Process $actionUrl 
        }
    }
    
    Write-Host "`n=== Debug Information ===" -ForegroundColor Cyan
    Write-Host "Check browser console (F12 -> Console) for debug logs"
    Write-Host "Look for:"
    Write-Host "- 'PDF Viewer script starting...'"
    Write-Host "- 'All URL parameters: ...'"
    Write-Host "- 'Extracted parameters: ...'"
    Write-Host ""
    Write-Host "If you see 'Missing parameters' error, the JavaScript parameters are not being parsed correctly."
    
} catch {
    Write-Host "Error: $_" -ForegroundColor Red
}