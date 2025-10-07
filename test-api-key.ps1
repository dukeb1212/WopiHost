# Test API Key Authentication for WopiHost
# This script demonstrates how to use the API key to access the WopiHost API

# Define API Key
$apiKey = "your-api-key-here" # Replace with your actual API key from appsettings.json

# Define base URL
$baseUrl = "http://localhost:5000" # Update with your actual host URL

# Example 1: Get action URL with API key in header
Write-Host "Testing API key in header..."
$headers = @{
    "X-API-Key" = $apiKey
}
$response = Invoke-RestMethod -Uri "$baseUrl/api/action/1?action=view" -Headers $headers -Method Get -ErrorAction SilentlyContinue
Write-Host "Response: $($response | ConvertTo-Json -Depth 5)"

# Example 2: Get action URL with API key in query string
Write-Host "`nTesting API key in query string..."
$response = Invoke-RestMethod -Uri "$baseUrl/api/action/1?action=view&api-key=$apiKey" -Method Get -ErrorAction SilentlyContinue
Write-Host "Response: $($response | ConvertTo-Json -Depth 5)"

# Example 3: Test without API key (should fail with 401 Unauthorized)
Write-Host "`nTesting without API key (should fail)..."
try {
    $response = Invoke-RestMethod -Uri "$baseUrl/api/action/1?action=view" -Method Get -ErrorAction Stop
    Write-Host "Response: $($response | ConvertTo-Json -Depth 5)"
} catch {
    Write-Host "Expected error: $($_.Exception.Response.StatusCode) - $($_.Exception.Message)"
}

Write-Host "`nTest completed."