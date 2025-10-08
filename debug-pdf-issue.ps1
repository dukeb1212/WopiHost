# Script debug cho PDF viewer issue
Write-Host "Debugging PDF Viewer Issue..." -ForegroundColor Cyan

$baseUrl = "http://localhost:5000"
$apiKey = "v6MQZpQHjh7BCm6j59Jj70oqJTAM+fV1phxgb4VAWwk="
$fileId = 55
$userId = 3

$headers = @{
    "X-API-Key" = $apiKey
}

# 1. Get Action URL
Write-Host "`n1. Getting Action URL from API..." -ForegroundColor Yellow
try {
    $actionResponse = Invoke-RestMethod -Uri "$baseUrl/api/action/$fileId`?action=view&userId=$userId" -Headers $headers -Method Get -UseBasicParsing -ErrorAction Stop
    Write-Host "Action URL Response:" -ForegroundColor Green
    $actionResponse | Format-List
    
    $actionUrl = $actionResponse.ActionUrl
    $accessToken = $actionResponse.AccessToken
    
    Write-Host "Generated Action URL: $actionUrl" -ForegroundColor White
    
} catch {
    Write-Host "Error getting action URL: $_" -ForegroundColor Red
    exit
}

# 2. Test direct PDF API call
Write-Host "`n2. Testing direct PDF API call..." -ForegroundColor Yellow
try {
    $pdfApiUrl = "$baseUrl/api/pdf/$fileId`?access_token=$accessToken"
    Write-Host "PDF API URL: $pdfApiUrl" -ForegroundColor White
    
    $pdfResponse = Invoke-WebRequest -Uri $pdfApiUrl -Headers $headers -UseBasicParsing -ErrorAction Stop
    Write-Host "PDF API Success!" -ForegroundColor Green
    Write-Host "Content-Type: $($pdfResponse.Headers['Content-Type'])" -ForegroundColor Green
    Write-Host "Content-Length: $($pdfResponse.Headers['Content-Length'])" -ForegroundColor Green
    
} catch {
    Write-Host "Error calling PDF API: $_" -ForegroundColor Red
    Write-Host "StatusCode: $($_.Exception.Response.StatusCode)" -ForegroundColor Red
}

# 3. Test PDF Viewer page
Write-Host "`n3. Testing PDF Viewer page..." -ForegroundColor Yellow
try {
    $viewerResponse = Invoke-WebRequest -Uri "$baseUrl/viewers/pdf.html" -UseBasicParsing -ErrorAction Stop
    Write-Host "PDF Viewer page accessible!" -ForegroundColor Green
    Write-Host "Content-Length: $($viewerResponse.Content.Length)" -ForegroundColor Green
    
} catch {
    Write-Host "Error accessing PDF Viewer page: $_" -ForegroundColor Red
}

# 4. Test with individual parameters
Write-Host "`n4. Testing PDF Viewer with parameters..." -ForegroundColor Yellow
$fileName = "Sinh mã tùy biến toàn diện_.pdf"
$encodedFileName = [System.Web.HttpUtility]::UrlEncode($fileName)
$encodedToken = [System.Web.HttpUtility]::UrlEncode($accessToken)

$testViewerUrl = "$baseUrl/viewers/pdf.html?fileId=$fileId&access_token=$encodedToken&fileName=$encodedFileName"
Write-Host "Test Viewer URL: $testViewerUrl" -ForegroundColor White

try {
    $testResponse = Invoke-WebRequest -Uri $testViewerUrl -UseBasicParsing -ErrorAction Stop
    Write-Host "PDF Viewer with parameters accessible!" -ForegroundColor Green
    
    # Check if the response contains "Missing parameters"
    if ($testResponse.Content -like "*Missing parameters*") {
        Write-Host "WARNING: Response contains 'Missing parameters'" -ForegroundColor Yellow
    } else {
        Write-Host "No 'Missing parameters' found in response" -ForegroundColor Green
    }
    
} catch {
    Write-Host "Error accessing PDF Viewer with parameters: $_" -ForegroundColor Red
}

# 5. Test the actual action URL
Write-Host "`n5. Testing actual Action URL..." -ForegroundColor Yellow
try {
    $actionUrlResponse = Invoke-WebRequest -Uri $actionUrl -UseBasicParsing -ErrorAction Stop
    Write-Host "Action URL accessible!" -ForegroundColor Green
    
    # Check if the response contains "Missing parameters"
    if ($actionUrlResponse.Content -like "*Missing parameters*") {
        Write-Host "WARNING: Action URL response contains 'Missing parameters'" -ForegroundColor Yellow
    } else {
        Write-Host "No 'Missing parameters' found in action URL response" -ForegroundColor Green
    }
    
} catch {
    Write-Host "Error accessing Action URL: $_" -ForegroundColor Red
}

Write-Host "`n=== URLs for manual testing ===" -ForegroundColor Cyan
Write-Host "Action URL: $actionUrl" -ForegroundColor White
Write-Host "Direct PDF API: $pdfApiUrl" -ForegroundColor White
Write-Host "Manual Viewer URL: $testViewerUrl" -ForegroundColor White

Write-Host "`nDebugging complete." -ForegroundColor Cyan