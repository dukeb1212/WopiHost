# Test Rate Limiting for WopiHost
# This script demonstrates the rate limiting by sending multiple requests in a short time

# Define API Key
$apiKey = "v6MQZpQHjh7BCm6j59Jj70oqJTAM+fV1phxgb4VAWwk=" # Replace with your actual API key from appsettings.json

# Define base URL
$baseUrl = "http://localhost:5000" # Update with your actual host URL

# Function to make API request
function Invoke-RateLimitedRequest {
    param (
        [string]$Url,
        [string]$ApiKey
    )
    
    try {
        $headers = @{
            "X-API-Key" = $ApiKey
        }
        $response = Invoke-RestMethod -Uri $Url -Headers $headers -Method Get -TimeoutSec 5 -ErrorAction Stop
        return @{
            Success = $true
            Response = $response
        }
    }
    catch {
        return @{
            Success = $false
            Error = $_.Exception
            StatusCode = $_.Exception.Response.StatusCode.value__
        }
    }
}

Write-Host "Testing Rate Limiting..."
Write-Host "Sending 120 requests (exceeds the 100 per minute limit)..."

$results = @{
    Success = 0
    RateLimited = 0
    OtherErrors = 0
}

# Send requests in a loop
for ($i = 1; $i -le 120; $i++) {
    $result = Invoke-RateLimitedRequest -Url "$baseUrl/api/action/1?action=view" -ApiKey $apiKey
    
    if ($result.Success) {
        $results.Success++
        Write-Host "." -NoNewline
    }
    elseif ($result.StatusCode -eq 429) {
        $results.RateLimited++
        Write-Host "L" -NoNewline -ForegroundColor Red
    }
    else {
        $results.OtherErrors++
        Write-Host "E" -NoNewline -ForegroundColor Yellow
    }
    
    # Small delay to not overwhelm the server completely
    Start-Sleep -Milliseconds 50
}

Write-Host "`n"
Write-Host "Rate Limiting Test Results:"
Write-Host "------------------------"
Write-Host "Successful requests: $($results.Success)"
Write-Host "Rate limited requests (429): $($results.RateLimited)"
Write-Host "Other errors: $($results.OtherErrors)"

if ($results.RateLimited -gt 0) {
    Write-Host "`nRate limiting is working correctly! Some requests were rate-limited as expected." -ForegroundColor Green
}
else {
    Write-Host "`nRate limiting may not be working correctly. No requests were rate-limited." -ForegroundColor Yellow
}

Write-Host "`nTest completed."