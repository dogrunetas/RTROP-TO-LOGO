using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using RTROPToLogoIntegration.Domain.Entities;
using RTROPToLogoIntegration.Domain.Models;
using RTROPToLogoIntegration.Infrastructure.Identity;
using Serilog;

namespace RTROPToLogoIntegration.Infrastructure.Identity
{
    /// <summary>
    /// JWT Access Token + Refresh Token yönetim servisi.
    /// Refresh Token Rotation: Her kullanımda eski token iptal edilir, yenisi üretilir.
    /// </summary>
    public class TokenService : ITokenService
    {
        private readonly IConfiguration _configuration;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly AppIdentityDbContext _dbContext;

        // Refresh token ömrü (gün cinsinden). appsettings'ten de alınabilir.
        // private const int REFRESH_TOKEN_LIFETIME_DAYS = 7;
        private int RefreshTokenLifetimeDays => int.Parse(_configuration["JwtSettings:RefreshTokenExpirationDays"] ?? "7");

        public TokenService(IConfiguration configuration, UserManager<IdentityUser> userManager, AppIdentityDbContext dbContext)
        {
            _configuration = configuration;
            _userManager = userManager;
            _dbContext = dbContext;
        }

        /// <summary>
        /// Login sırasında çağrılır. Access + Refresh token üretir, refresh token'ı DB'ye kaydeder.
        /// </summary>
        public async Task<AuthResponse> GenerateTokenAsync(IdentityUser user, string? ipAddress = null)
        {
            // Single Session: Önceki tüm aktif tokenları iptal et
            await RevokeAllTokensAsync(user.Id);

            var accessToken = await GenerateAccessTokenAsync(user);
            var refreshToken = GenerateRefreshTokenString();

            // Refresh token'ı DB'ye kaydet
            var refreshTokenEntity = new UserRefreshToken
            {
                UserId = user.Id,
                Token = refreshToken,
                ExpiresAt = DateTime.UtcNow.AddDays(RefreshTokenLifetimeDays),
                CreatedAt = DateTime.UtcNow,
                CreatedByIp = ipAddress
            };

            _dbContext.UserRefreshTokens.Add(refreshTokenEntity);
            await _dbContext.SaveChangesAsync();

            var expirationMinutes = double.Parse(_configuration["JwtSettings:ExpirationInMinutes"]);

            return new AuthResponse
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                Expiration = DateTime.Now.AddMinutes(expirationMinutes)
            };
        }

        /// <summary>
        /// Refresh token ile yeni token seti üretir (Token Rotation).
        /// Eski refresh token iptal edilir, yeni bir refresh token üretilip DB'ye kaydedilir.
        /// </summary>
        public async Task<AuthResponse?> RefreshTokenAsync(string accessToken, string refreshToken, string? ipAddress = null)
        {
            // 1. Access token'dan kullanıcı bilgisini çıkar (süresi dolmuş olsa bile)
            var principal = GetPrincipalFromExpiredToken(accessToken);
            if (principal == null)
            {
                Log.Warning("Refresh token isteği başarısız: Access token parse edilemedi.");
                return null;
            }

            var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                Log.Warning("Refresh token isteği başarısız: UserId claim bulunamadı.");
                return null;
            }

            // 2. DB'den refresh token'ı bul
            var storedToken = await _dbContext.UserRefreshTokens
                .FirstOrDefaultAsync(t => t.Token == refreshToken && t.UserId == userId);

            if (storedToken == null)
            {
                Log.Warning("Refresh token bulunamadı. UserId: {UserId}", userId);
                return null;
            }

            // 3. Token aktif mi kontrol et
            if (!storedToken.IsActive)
            {
                // Eğer çalınmış bir token tekrar kullanılıyorsa, güvenlik önlemi olarak
                // kullanıcının TÜM refresh token'larını iptal et.
                if (storedToken.RevokedAt != null)
                {
                    Log.Warning("Kullanılmış refresh token tekrar denendi! Tüm tokenlar iptal ediliyor. UserId: {UserId}", userId);
                    await RevokeAllTokensAsync(userId);
                }
                else
                {
                    Log.Warning("Süresi dolmuş refresh token kullanıldı. UserId: {UserId}", userId);
                }
                return null;
            }

            // 4. Single Session: Kullanıcının TÜM aktif tokenlarını iptal et
            await RevokeAllTokensAsync(userId);

            // 5. Yeni refresh token oluştur
            var newRefreshTokenString = GenerateRefreshTokenString();

            // 6. Yeni refresh token oluştur ve kaydet
            var newRefreshToken = new UserRefreshToken
            {
                UserId = userId,
                Token = newRefreshTokenString,
                ExpiresAt = DateTime.UtcNow.AddDays(RefreshTokenLifetimeDays),
                CreatedAt = DateTime.UtcNow,
                CreatedByIp = ipAddress
            };

            _dbContext.UserRefreshTokens.Add(newRefreshToken);
            await _dbContext.SaveChangesAsync();

            // 7. Yeni access token üret
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                Log.Warning("Refresh token isteği başarısız: Kullanıcı bulunamadı. UserId: {UserId}", userId);
                return null;
            }

            var newAccessToken = await GenerateAccessTokenAsync(user);
            var expirationMinutes = double.Parse(_configuration["JwtSettings:ExpirationInMinutes"]);

            Log.Information("Token başarıyla yenilendi. UserId: {UserId}", userId);

            return new AuthResponse
            {
                AccessToken = newAccessToken,
                RefreshToken = newRefreshTokenString,
                Expiration = DateTime.Now.AddMinutes(expirationMinutes)
            };
        }

        /// <summary>
        /// Kullanıcının tüm aktif refresh token'larını iptal eder.
        /// </summary>
        public async Task RevokeAllTokensAsync(string userId)
        {
            var activeTokens = await _dbContext.UserRefreshTokens
                .Where(t => t.UserId == userId && t.RevokedAt == null)
                .ToListAsync();

            foreach (var token in activeTokens)
            {
                token.RevokedAt = DateTime.UtcNow;
            }

            await _dbContext.SaveChangesAsync();
            Log.Information("Kullanıcının tüm refresh token'ları iptal edildi. UserId: {UserId}, İptal Edilen: {Count}", userId, activeTokens.Count);
        }

        // ============ PRIVATE METHODS ============

        /// <summary>
        /// JWT Access Token üretir.
        /// </summary>
        private async Task<string> GenerateAccessTokenAsync(IdentityUser user)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Name, user.UserName),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var roles = await _userManager.GetRolesAsync(user);
            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["JwtSettings:SecretKey"]));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var expirationMinutes = double.Parse(_configuration["JwtSettings:ExpirationInMinutes"]);

            var token = new JwtSecurityToken(
                issuer: _configuration["JwtSettings:Issuer"],
                audience: _configuration["JwtSettings:Audience"],
                claims: claims,
                expires: DateTime.Now.AddMinutes(expirationMinutes),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        /// <summary>
        /// Kriptografik olarak güvenli random refresh token string üretir.
        /// </summary>
        private string GenerateRefreshTokenString()
        {
            var randomNumber = new byte[64];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomNumber);
            return Convert.ToBase64String(randomNumber);
        }

        /// <summary>
        /// Süresi dolmuş bir access token'dan ClaimsPrincipal çıkarır.
        /// Refresh token flow'unda kullanılır (süresi dolmuş token kabul edilir).
        /// </summary>
        private ClaimsPrincipal? GetPrincipalFromExpiredToken(string token)
        {
            try
            {
                var tokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = false, // CRITICAL: Süresi dolmuş token'ı da kabul et
                    ValidIssuer = _configuration["JwtSettings:Issuer"],
                    ValidAudience = _configuration["JwtSettings:Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(_configuration["JwtSettings:SecretKey"]))
                };

                var tokenHandler = new JwtSecurityTokenHandler();
                var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out var securityToken);

                if (securityToken is not JwtSecurityToken jwtToken ||
                    !jwtToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
                {
                    return null;
                }

                return principal;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Access token parse hatası.");
                return null;
            }
        }
    }
}
