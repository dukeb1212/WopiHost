#nullable enable
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using WopiHost.Services;
using System.Net.Mime;
using Microsoft.AspNetCore.Http.Extensions;

namespace WopiHost.Controllers;

[ApiController]
[Route("viewers/[controller]")]
public class PdfProxyController : ControllerBase
{
    private readonly IFileService _fileService;
    private readonly IJwtService _jwtService;
    private readonly ILogger<PdfProxyController> _logger;

    public PdfProxyController(
        IFileService fileService,
        IJwtService jwtService,
        ILogger<PdfProxyController> logger)
    {
        _fileService = fileService;
        _jwtService = jwtService;
        _logger = logger;
    }

    /// <summary>
    /// Proxy endpoint để PDF.js có thể tải PDF mà không bị encode URL
    /// </summary>
    /// <param name="fileId">ID của file</param>
    /// <param name="token">JWT token (không encoded)</param>
    /// <returns>Nội dung file PDF</returns>
    [HttpGet("{fileId}/{tokenBase64}")]
    [HttpPost("{fileId}")]
    public async Task<IActionResult> GetPdfProxyPath(int fileId, string? tokenBase64 = null, [FromQuery] string? token = null, [FromForm] string? accessToken = null)
    {
        try
        {
            _logger.LogInformation("=== PDF PROXY REQUEST ===");
            _logger.LogInformation("FileId: {FileId}", fileId);
            _logger.LogInformation("TokenBase64 length: {Length}", tokenBase64?.Length ?? 0);
            _logger.LogInformation("TokenBase64 preview: {Preview}", 
                tokenBase64?.Substring(0, Math.Min(50, tokenBase64?.Length ?? 0)) ?? "null");
            _logger.LogInformation("Request URL: {Url}", HttpContext.Request.GetDisplayUrl());
            _logger.LogInformation("Request Headers: {Headers}", 
                string.Join(", ", HttpContext.Request.Headers.Select(h => $"{h.Key}={h.Value}")));
            // Support multiple token sources: path (base64), query string, form data
            string actualToken = string.Empty;

            if (!string.IsNullOrEmpty(tokenBase64))
            {
                try
                {
                    actualToken = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(tokenBase64));
                    _logger.LogDebug("Decoded token from base64 path parameter");
                    _logger.LogInformation("Token decoded successfully, length: {Length}", actualToken.Length);
                    _logger.LogInformation("Token preview: {Preview}", actualToken.Substring(0, Math.Min(50, actualToken.Length)));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to decode base64 token from path");
                }
            }

            if (string.IsNullOrEmpty(actualToken))
            {
                actualToken = accessToken ?? token ?? string.Empty;
            }

            _logger.LogInformation("PDF Proxy {Method} request for file {FileId} with token length {TokenLength}",
                Request.Method, fileId, actualToken?.Length ?? 0);

            if (string.IsNullOrEmpty(actualToken))
            {
                _logger.LogWarning("Token is null or empty for PDF proxy request");
                return BadRequest("Token is required");
            }

            _logger.LogDebug("Validating token: {TokenPreview}...", actualToken.Length > 10 ? actualToken.Substring(0, 10) : actualToken);

            // Validate token
            var tokenInfo = _jwtService.ValidateToken(actualToken);
            if (tokenInfo == null)
            {
                _logger.LogWarning("Token validation failed for PDF proxy request - token: {TokenPreview}...", actualToken.Length > 10 ? actualToken.Substring(0, 10) : actualToken);
                return Unauthorized("Invalid token");
            }

            _logger.LogInformation("Token validated successfully for PDF proxy request");

            // Check file ID in token
            var fileIdClaim = tokenInfo.Claims.FirstOrDefault(c => c.Type == "file_id")?.Value;
            if (string.IsNullOrEmpty(fileIdClaim) || !fileIdClaim.Equals(fileId.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Token file ID mismatch for PDF proxy request");
                return Unauthorized("Token does not match requested file");
            }

            // Get file info
            var file = await _fileService.GetFileByIdAsync(fileId);
            if (file == null)
            {
                return NotFound("File not found");
            }

            if (!file.FileExtension.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest("File is not a PDF");
            }

            // Get file stream
            var fileStream = await _fileService.GetFileStreamAsync(fileId);
            if (fileStream == null)
            {
                return NotFound("File content not available");
            }

            // Set headers để tránh cache và đảm bảo đúng content type
            Response.Headers["Content-Disposition"] = "inline";
            Response.Headers["X-Content-Type-Options"] = "nosniff";
            Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
            Response.Headers["Pragma"] = "no-cache";
            Response.Headers["Expires"] = "0";
            Response.Headers["Access-Control-Allow-Origin"] = "*";
            Response.Headers["Access-Control-Allow-Methods"] = "GET";

            _logger.LogInformation("Returning PDF content via proxy for file {FileId}", fileId);
            return File(fileStream, MediaTypeNames.Application.Pdf);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in PDF proxy for file {FileId}", fileId);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetPdfProxyQuery([FromQuery] int fileId, [FromQuery] string token)
        {
            try
            {
                _logger.LogInformation("PDF proxy request - FileId: {FileId}, Token length: {TokenLength}", 
                    fileId, token?.Length ?? 0);

                if (fileId <= 0)
                {
                    _logger.LogWarning("Invalid fileId: {FileId}", fileId);
                    return BadRequest("Invalid fileId parameter");
                }

                if (string.IsNullOrEmpty(token))
                {
                    _logger.LogWarning("Missing token parameter");
                    return BadRequest("Missing token parameter");
                }

                // Validate JWT token
                var tokenData = _jwtService.ValidateToken(token);
                if (tokenData == null)
                {
                    _logger.LogWarning("Invalid JWT token for fileId: {FileId}", fileId);
                    return Unauthorized("Invalid token");
                }

                // Verify fileId matches token
                var tokenFileId = tokenData.Claims.FirstOrDefault(c => c.Type == "file_id")?.Value;
                if (string.IsNullOrEmpty(tokenFileId) || tokenFileId != fileId.ToString())
                {
                    _logger.LogWarning("FileId mismatch. URL: {FileId}, Token: {TokenFileId}", 
                        fileId, tokenFileId);
                    return Unauthorized("FileId mismatch");
                }

                // Get file stream from service
                var fileStream = await _fileService.GetFileStreamAsync(fileId);
                if (fileStream == null)
                {
                    _logger.LogWarning("File not found: {FileId}", fileId);
                    return NotFound("File not found");
                }

                _logger.LogInformation("Returning PDF content via proxy for file {FileId}", fileId);

                return File(fileStream, "application/pdf");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in PDF proxy for fileId: {FileId}", fileId);
                return StatusCode(500, "Internal server error");
            }
        }
}