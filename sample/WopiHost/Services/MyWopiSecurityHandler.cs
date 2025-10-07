using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using WopiHost.Abstractions;
using WopiHost.Models.Configuration;

namespace WopiHost.Services
{
    /// <summary>
    /// Triển khai của IWopiSecurityHandler sử dụng JWT để xác thực và phân quyền.
    /// </summary>
    public class MyWopiSecurityHandler : IWopiSecurityHandler
    {
#nullable enable
        private readonly IJwtService _jwtService;
        private readonly JwtSettings _jwtSettings;
        private readonly ILogger<MyWopiSecurityHandler> _logger;

        public MyWopiSecurityHandler(IJwtService jwtService, IOptions<JwtSettings> jwtSettings, ILogger<MyWopiSecurityHandler> logger)
        {
            _jwtService = jwtService;
            _jwtSettings = jwtSettings.Value;
            _logger = logger;
        }

        /// <summary>
        /// Tạo token truy cập cho một người dùng và tài nguyên cụ thể.
        /// </summary>
        public Task<SecurityToken> GenerateAccessToken(string userId, string resourceId, CancellationToken cancellationToken = default)
        {
            try
            {
                if (!int.TryParse(resourceId, out var fileId))
                {
                    throw new ArgumentException("ResourceId phải là một số nguyên hợp lệ", nameof(resourceId));
                }

                var tokenHandler = new JwtSecurityTokenHandler();
                var tokenDescriptor = new SecurityTokenDescriptor
                {
                    Subject = new ClaimsIdentity(new[]
                    {
                        new Claim("file_id", resourceId),
                        new Claim("user_id", userId),
                        new Claim(ClaimTypes.NameIdentifier, userId)
                    }),
                    Expires = DateTime.UtcNow.AddMinutes(_jwtSettings.ExpirationMinutes),
                    Issuer = _jwtSettings.Issuer,
                    Audience = _jwtSettings.Audience,
                    SigningCredentials = new SigningCredentials(
                        new SymmetricSecurityKey(System.Text.Encoding.ASCII.GetBytes(_jwtSettings.SecretKey)), 
                        SecurityAlgorithms.HmacSha256Signature)
                };

                return Task.FromResult(tokenHandler.CreateToken(tokenDescriptor));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Không thể tạo access token cho userId {UserId} và resourceId {ResourceId}", userId, resourceId);
                throw;
            }
        }

        /// <summary>
        /// Lấy thông tin người dùng từ token.
        /// </summary>
        public Task<ClaimsPrincipal?> GetPrincipal(string token, CancellationToken cancellationToken = default)
        {
            try
            {
                var principal = _jwtService.ValidateToken(token);
                return Task.FromResult(principal);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Không thể trích xuất principal từ token");
                return Task.FromResult<ClaimsPrincipal?>(null);
            }
        }

        /// <summary>
        /// Kiểm tra quyền của người dùng cho một hành động cụ thể.
        /// </summary>
        public Task<bool> IsAuthorized(ClaimsPrincipal principal, IWopiAuthorizationRequirement requirement, CancellationToken cancellationToken = default)
        {
            if (principal?.Identity?.IsAuthenticated != true)
            {
                return Task.FromResult(false);
            }

            // Kiểm tra resource ID nếu có
            if (!string.IsNullOrEmpty(requirement.ResourceId))
            {
                var fileIdClaim = principal.FindFirst("file_id");
                if (fileIdClaim == null || fileIdClaim.Value != requirement.ResourceId)
                {
                    _logger.LogWarning("Người dùng không được phép truy cập tài nguyên {ResourceId}", requirement.ResourceId);
                    return Task.FromResult(false);
                }
            }

            // Kiểm tra quyền thực hiện hành động
            // Có thể thêm logic phân quyền chi tiết hơn tại đây
            return Task.FromResult(true);
        }

        /// <summary>
        /// Chuyển đổi SecurityToken thành chuỗi.
        /// </summary>
        public string WriteToken(SecurityToken token)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            return tokenHandler.WriteToken(token);
        }

        /// <summary>
        /// Lấy quyền của người dùng trên file cụ thể.
        /// </summary>
        public Task<WopiUserPermissions> GetUserPermissions(ClaimsPrincipal principal, IWopiFile file, CancellationToken cancellationToken = default)
        {
            if (principal?.Identity?.IsAuthenticated != true)
            {
                return Task.FromResult(WopiUserPermissions.ReadOnly | WopiUserPermissions.RestrictedWebViewOnly);
            }

            // Mặc định, cho phép tất cả các quyền đối với người dùng đã xác thực
            var permissions = WopiUserPermissions.UserCanWrite |
                             WopiUserPermissions.UserCanRename |
                             WopiUserPermissions.UserCanAttend |
                             WopiUserPermissions.UserCanPresent;

            return Task.FromResult(permissions);
        }
    }
}
