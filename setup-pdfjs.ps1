# Script để tải và cài đặt PDF.js cho WopiHost
Write-Host "Bắt đầu cài đặt PDF.js..." -ForegroundColor Cyan

# Tạo thư mục tạm thời
$tempDir = ".\temp-pdfjs"
New-Item -ItemType Directory -Path $tempDir -Force | Out-Null

# Đường dẫn đến thư mục wwwroot/lib/pdfjs
$targetDir = ".\sample\WopiHost\wwwroot\lib\pdfjs"

# Tạo thư mục đích nếu chưa tồn tại
if (-not (Test-Path $targetDir)) {
    Write-Host "Tạo thư mục $targetDir..." -ForegroundColor Yellow
    New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
}

try {
    # Tải PDF.js từ GitHub
    $pdfJsVersion = "5.4.296" # Phiên bản ổn định mới nhất
    $downloadUrl = "https://github.com/mozilla/pdf.js/releases/download/v$pdfJsVersion/pdfjs-$pdfJsVersion-dist.zip"
    $zipFile = "$tempDir\pdfjs.zip"
    
    Write-Host "Tải PDF.js v$pdfJsVersion từ GitHub..." -ForegroundColor Yellow
    Invoke-WebRequest -Uri $downloadUrl -OutFile $zipFile -UseBasicParsing
    
    # Giải nén file
    Write-Host "Giải nén PDF.js..." -ForegroundColor Yellow
    Expand-Archive -Path $zipFile -DestinationPath $tempDir -Force
    
    # Sao chép các file cần thiết
    Write-Host "Sao chép các file PDF.js vào $targetDir..." -ForegroundColor Yellow
    
    # Sao chép thư mục build
    if (Test-Path "$tempDir\build") {
        Copy-Item -Path "$tempDir\build" -Destination "$targetDir\" -Recurse -Force
    } else {
        Copy-Item -Path "$tempDir\pdfjs-$pdfJsVersion-dist\build" -Destination "$targetDir\" -Recurse -Force
    }
    
    # Sao chép thư mục web
    if (Test-Path "$tempDir\web") {
        Copy-Item -Path "$tempDir\web" -Destination "$targetDir\" -Recurse -Force
    } else {
        Copy-Item -Path "$tempDir\pdfjs-$pdfJsVersion-dist\web" -Destination "$targetDir\" -Recurse -Force
    }
    
    Write-Host "Cài đặt PDF.js thành công!" -ForegroundColor Green
    
} catch {
    Write-Host "Lỗi khi cài đặt PDF.js: $_" -ForegroundColor Red
} finally {
    # Dọn dẹp
    if (Test-Path $tempDir) {
        Write-Host "Dọn dẹp thư mục tạm..." -ForegroundColor Yellow
        Remove-Item -Path $tempDir -Recurse -Force
    }
}

Write-Host "Hoàn tất cài đặt PDF.js." -ForegroundColor Cyan