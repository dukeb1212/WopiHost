# Debugging script để kiểm tra trình xem PDF
Write-Host "Bắt đầu kiểm tra trình xem PDF..." -ForegroundColor Cyan

$baseUrl = "http://localhost:5000"
$apiKey = "v6MQZpQHjh7BCm6j59Jj70oqJTAM+fV1phxgb4VAWwk="
$fileId = 55
$userId = 3

# Headers
$headers = @{
    "X-API-Key" = $apiKey
}

# 1. Lấy Action URL từ API
Write-Host "`nKiểm tra API action để lấy URL xem PDF..." -ForegroundColor Yellow
try {
    $response = Invoke-RestMethod -Uri "$baseUrl/api/action/$fileId`?action=view&userId=$userId" -Headers $headers -Method Get -UseBasicParsing -ErrorAction Stop
    Write-Host "API Action trả về thành công:" -ForegroundColor Green
    $response | Format-List
    
    $pdfUrl = $response.ActionUrl
    $accessToken = $response.AccessToken
} catch {
    Write-Host "Lỗi khi gọi API action: $_" -ForegroundColor Red
    exit
}

# 2. Kiểm tra trực tiếp API PDF
Write-Host "`nKiểm tra trực tiếp API PDF..." -ForegroundColor Yellow
try {
    $pdfApiUrl = "$baseUrl/api/pdf/$fileId`?access_token=$accessToken"
    Write-Host "Gọi API PDF: $pdfApiUrl" -ForegroundColor Yellow
    
    $pdfApiResponse = Invoke-WebRequest -Uri $pdfApiUrl -Headers $headers -UseBasicParsing -ErrorAction Stop
    Write-Host "API PDF trả về thành công:" -ForegroundColor Green
    Write-Host "Content-Type: $($pdfApiResponse.Headers["Content-Type"])" -ForegroundColor Green
    Write-Host "Content-Length: $($pdfApiResponse.Headers["Content-Length"])" -ForegroundColor Green
} catch {
    Write-Host "Lỗi khi gọi API PDF: $_" -ForegroundColor Red
}

# 3. Kiểm tra các URLs cho mỗi trình xem
Write-Host "`nURLs cho các trình xem PDF:" -ForegroundColor Yellow

# URL gốc từ API
Write-Host "URL gốc từ API:" -ForegroundColor White
Write-Host $pdfUrl -ForegroundColor Cyan

# URL cho direct viewer
$directUrl = "$baseUrl/viewers/pdf.html?fileId=$fileId&access_token=$accessToken&viewer=direct"
Write-Host "`nURL cho direct viewer:" -ForegroundColor White
Write-Host $directUrl -ForegroundColor Cyan

# URL cho PDF.js viewer
$pdfJsUrl = "$baseUrl/viewers/pdf.html?fileId=$fileId&access_token=$accessToken&viewer=pdfjs"
Write-Host "`nURL cho PDF.js viewer:" -ForegroundColor White
Write-Host $pdfJsUrl -ForegroundColor Cyan

# URL cho simple viewer
$simpleUrl = "$baseUrl/viewers/pdf.html?fileId=$fileId&access_token=$accessToken&viewer=simple"
Write-Host "`nURL cho simple viewer:" -ForegroundColor White
Write-Host $simpleUrl -ForegroundColor Cyan

# 4. Mở trình xem mặc định
Write-Host "`nMở trình xem mặc định trong trình duyệt?" -ForegroundColor Yellow
$openBrowser = Read-Host "Nhập Y để mở (Y/N)"

if ($openBrowser -eq "Y" -or $openBrowser -eq "y") {
    Start-Process $pdfUrl
    Write-Host "Đã mở trình xem mặc định" -ForegroundColor Green
}

Write-Host "`nKiểm tra hoàn tất." -ForegroundColor Cyan