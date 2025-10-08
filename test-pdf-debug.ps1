# Debugging script để kiểm tra PDF.js viewer
Write-Host "Bắt đầu kiểm tra trình xem PDF..." -ForegroundColor Cyan

$baseUrl = "http://localhost:5000"
$apiKey = "v6MQZpQHjh7BCm6j59Jj70oqJTAM+fV1phxgb4VAWwk="
$fileId = 55
$userId = 3

# Headers
$headers = @{
    "X-API-Key" = $apiKey
}

# 1. Kiểm tra API action
Write-Host "`nKiểm tra API action để lấy URL xem PDF..." -ForegroundColor Yellow
try {
    $response = Invoke-RestMethod -Uri "$baseUrl/api/action/$fileId`?action=view&userId=$userId" -Headers $headers -Method Get -ErrorAction Stop
    Write-Host "API Action trả về thành công:" -ForegroundColor Green
    $response | Format-List
    
    $pdfUrl = $response.ActionUrl
    $accessToken = $response.AccessToken
} catch {
    Write-Host "Lỗi khi gọi API action: $_" -ForegroundColor Red
    exit
}

# 2. Kiểm tra API PDF
Write-Host "`nKiểm tra trực tiếp API PDF..." -ForegroundColor Yellow
try {
    $pdfApiUrl = "$baseUrl/api/pdf/$fileId`?access_token=$accessToken"
    Write-Host "Gọi API PDF: $pdfApiUrl" -ForegroundColor Yellow
    
    $pdfApiResponse = Invoke-WebRequest -Uri $pdfApiUrl -Headers $headers -UseBasicParsing -ErrorAction Stop
    Write-Host "API PDF trả về thành công:" -ForegroundColor Green
    Write-Host "Content-Type: $($pdfApiResponse.Headers['Content-Type'])" -ForegroundColor Green
    Write-Host "Content-Length: $($pdfApiResponse.Headers['Content-Length'])" -ForegroundColor Green
} catch {
    Write-Host "Lỗi khi gọi API PDF: $_" -ForegroundColor Red
}

# 3. Kiểm tra các file PDF.js
Write-Host "`nKiểm tra các file PDF.js..." -ForegroundColor Yellow

$filesToCheck = @(
    "/viewers/pdf.html",
    "/viewers/pdf/viewer.html",
    "/lib/pdfjs/build/pdf.mjs",
    "/lib/pdfjs/web/viewer.mjs",
    "/lib/pdfjs/web/viewer.css"
)

foreach ($file in $filesToCheck) {
    try {
        $fileUrl = "$baseUrl$file"
        Write-Host "Kiểm tra file: $fileUrl" -ForegroundColor Yellow
        
        $fileResponse = Invoke-WebRequest -Uri $fileUrl -UseBasicParsing -ErrorAction Stop
        Write-Host "File có sẵn: $file (Status: $($fileResponse.StatusCode), Size: $($fileResponse.Content.Length) bytes)" -ForegroundColor Green
    } catch {
        Write-Host "Lỗi khi truy cập file: $file - $_" -ForegroundColor Red
    }
}

# 4. Xem thử URL PDF viewer
Write-Host "`nURL để xem PDF trong trình duyệt:" -ForegroundColor Yellow
Write-Host $pdfUrl -ForegroundColor White -BackgroundColor DarkBlue

Write-Host "`nKiểm tra hoàn tất." -ForegroundColor Cyan