# Test PDF Viewer trong WopiHost
# Script này kiểm tra chức năng xem file PDF

# Cấu hình
$apiKey = "v6MQZpQHjh7BCm6j59Jj70oqJTAM+fV1phxgb4VAWwk=" # Thay thế bằng API key thực tế từ appsettings.json
$baseUrl = "http://localhost:5000"  # Cập nhật URL thực tế của server
$fileId = 55  # ID của file PDF bạn muốn kiểm tra
$userId = 3   # ID người dùng hợp lệ trong hệ thống

# Headers
$headers = @{
    "X-API-Key" = $apiKey
}

Write-Host "Bắt đầu kiểm tra chức năng xem PDF..." -ForegroundColor Cyan

# Bước 1: Lấy URL xem file PDF
Write-Host "Lấy URL xem PDF từ API..." -ForegroundColor Yellow
try {
    $response = Invoke-RestMethod -Uri "$baseUrl/api/action/$fileId`?action=view&userId=$userId" -Headers $headers -Method Get -ErrorAction Stop
    
    Write-Host "Thành công! Nhận được URL xem file PDF: $($response.ActionUrl)" -ForegroundColor Green
    Write-Host "Thông tin phản hồi:" -ForegroundColor Green
    $response | Format-List
    
    # Nếu viewer type là PDF, tức là đã xử lý đúng
    if ($response.ViewerType -eq "pdf") {
        Write-Host "File được phát hiện và xử lý đúng là PDF!" -ForegroundColor Green
    } else {
        Write-Host "Cảnh báo: File không được xử lý như PDF" -ForegroundColor Yellow
    }
    
    $pdfUrl = $response.ActionUrl
} catch {
    Write-Host "Lỗi khi lấy URL xem PDF: $_" -ForegroundColor Red
    exit
}

# Bước 2: Kiểm tra truy cập API PDF
Write-Host "`nKiểm tra API PDF trực tiếp..." -ForegroundColor Yellow
try {
    $accessToken = $response.AccessToken
    $pdfApiUrl = "$baseUrl/api/pdf/$fileId`?access_token=$accessToken"
    
    $pdfResponse = Invoke-WebRequest -Uri $pdfApiUrl -Headers $headers -Method Get -ErrorAction Stop -UseBasicParsing
    
    Write-Host "Thành công! API PDF trả về dữ liệu với Content-Type: $($pdfResponse.Headers['Content-Type'])" -ForegroundColor Green
    Write-Host "Kích thước dữ liệu: $($pdfResponse.Content.Length) bytes" -ForegroundColor Green
    
    if ($pdfResponse.Headers['Content-Type'] -eq "application/pdf") {
        Write-Host "Content-Type hợp lệ (application/pdf)" -ForegroundColor Green
    } else {
        Write-Host "Cảnh báo: Content-Type không phải application/pdf" -ForegroundColor Yellow
    }
} catch {
    Write-Host "Lỗi khi truy cập API PDF: $_" -ForegroundColor Red
}

# Bước 3: Kiểm tra trang HTML viewer
Write-Host "`nKiểm tra trang HTML viewer..." -ForegroundColor Yellow
try {
    $viewerUrl = "$baseUrl/viewers/pdf.html"
    
    $viewerResponse = Invoke-WebRequest -Uri $viewerUrl -ErrorAction Stop -UseBasicParsing
    
    Write-Host "Thành công! Trang HTML viewer có sẵn" -ForegroundColor Green
    Write-Host "Kích thước HTML: $($viewerResponse.Content.Length) bytes" -ForegroundColor Green
} catch {
    Write-Host "Lỗi khi truy cập trang HTML viewer: $_" -ForegroundColor Red
}

Write-Host "`nĐể xem PDF, hãy truy cập URL này trong trình duyệt:" -ForegroundColor Cyan
Write-Host $pdfUrl -ForegroundColor White -BackgroundColor DarkBlue

Write-Host "`nKiểm tra hoàn tất." -ForegroundColor Cyan