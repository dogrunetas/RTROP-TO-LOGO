using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using RTROPToLogoIntegration.Domain.Entities;
using RTROPToLogoIntegration.Domain.Models;
using Serilog;

namespace RTROPToLogoIntegration.Infrastructure.Identity
{
    /// <summary>
    /// JWT + Refresh Token yönetimi.
    /// Token rotation, stolen token detection ve immediate invalidation sağlar.
    /// </summary>
    public class TokenService : ITokenService
    {
        private readonly IConfiguration _configuration;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly AppIdentityDbContext _dbContext;

        public TokenService(
            IConfiguration configuration,
            UserManager<IdentityUser> userManager,
            AppIdentityDbContext dbContext)
        {
            _configuration = configuration;
            _userManager = userManager;
            _dbContext = dbContext;
        }

        public async Task<AuthResponse> GenerateTokenAsync(IdentityUser user, string? ipAddress = null)
        {
            // 1. Önce mevcut tüm refresh token'ları revoke et (tek aktif oturum politikası)
            await RevokeAllUserRefreshTokensInternalAsync(user.Id);

            // 2. TokensRevokedAt güncelle ÖNCE (eski JWT'leri invalidate et)
            //    Böylece yeni üretilecek JWT'nin iat değeri >= TokensRevokedAt olur.
            await UpdateTokensRevokedAtAsync(user.Id);

            // 3. JWT Access Token oluştur (iat şimdi TokensRevokedAt'tan sonra)
            var (accessToken, expires) = GenerateAccessToken(user, await _userManager.GetRolesAsync(user));

            // 4. Refresh Token oluştur ve DB'ye kaydet
            var refreshTokenString = GenerateRefreshTokenString();
            var refreshTokenEntity = new UserRefreshToken
            {
                UserId = user.Id,
                Token = refreshTokenString,
                ExpiresAt = DateTime.UtcNow.AddDays(GetRefreshTokenExpirationDays()),
                CreatedAt = DateTime.UtcNow,
                CreatedByIp = ipAddress
            };

            _dbContext.UserRefreshTokens.Add(refreshTokenEntity);

            await _dbContext.SaveChangesAsync();

            Log.Information("Yeni token çifti üretildi. UserId: {UserId}, IP: {IpAddress}", user.Id, ipAddress);

            return new AuthResponse
            {
                AccessToken = accessToken,
                RefreshToken = refreshTokenString,
                Expiration = expires
            };
        }

        public async Task<AuthResponse?> RefreshTokenAsync(string accessToken, string refreshToken, string? ipAddress = null)
        {
            // 1. Expired access token'dan kullanıcı bilgisini çıkar
            var principal = GetPrincipalFromExpiredToken(accessToken);
            if (principal == null)
            {
                Log.Warning("Refresh token isteği: Access token parse edilemedi.");
                return null;
            }

            var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                Log.Warning("Refresh token isteği: UserId claim bulunamadı.");
                return null;
            }

            // 2. DB'den refresh token'ı bul
            var storedToken = await _dbContext.UserRefreshTokens
                .FirstOrDefaultAsync(t => t.Token == refreshToken && t.UserId == userId);

            if (storedToken == null)
            {
                Log.Warning("Refresh token isteği: Token DB'de bulunamadı. UserId: {UserId}", userId);
                return null;
            }

            // 3. STOLEN TOKEN DETECTION
            // Eğer token zaten revoke edilmişse, bu token çalınmış olabilir.
            // Tüm token ailesini (kullanıcının tüm token'larını) revoke et.
            if (storedToken.RevokedAt != null)
            {
                Log.Warning("GÜVENLIK ALARMI: Revoke edilmiş refresh token tekrar kullanıldı! UserId: {UserId}, IP: {IpAddress}. Tüm token'lar revoke ediliyor.", userId, ipAddress);
                await RevokeAllTokensAsync(userId);
                return null;
            }

            // 4. Süre kontrolü
            if (storedToken.IsExpired)
            {
                Log.Warning("Refresh token isteği: Token süresi dolmuş. UserId: {UserId}", userId);
                return null;
            }

            // 5. TOKEN ROTATION: Eski token'ı revoke et, yenisini oluştur
            var newRefreshTokenString = GenerateRefreshTokenString();

            storedToken.RevokedAt = DateTime.UtcNow;
            storedToken.ReplacedByToken = newRefreshTokenString;

            var newRefreshToken = new UserRefreshToken
            {
                UserId = userId,
                Token = newRefreshTokenString,
                ExpiresAt = DateTime.UtcNow.AddDays(GetRefreshTokenExpirationDays()),
                CreatedAt = DateTime.UtcNow,
                CreatedByIp = ipAddress
            };

            _dbContext.UserRefreshTokens.Add(newRefreshToken);

            // 6. Kullanıcıyı bul
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                Log.Warning("Refresh token isteği: Kullanıcı bulunamadı. UserId: {UserId}", userId);
                return null;
            }

            // 7. TokensRevokedAt güncelle ÖNCE (yeni JWT'nin iat'ı bundan sonra olacak)
            await UpdateTokensRevokedAtAsync(userId);

            // 8. Yeni Access Token üret (iat şimdi TokensRevokedAt'tan sonra)
            var (newAccessToken, expires) = GenerateAccessToken(user, await _userManager.GetRolesAsync(user));

            await _dbContext.SaveChangesAsync();

            Log.Information("Token başarıyla yenilendi. UserId: {UserId}, IP: {IpAddress}", userId, ipAddress);

            return new AuthResponse
            {
                AccessToken = newAccessToken,
                RefreshToken = newRefreshTokenString,
                Expiration = expires
            };
        }

        public async Task RevokeAllTokensAsync(string userId)
        {
            await RevokeAllUserRefreshTokensInternalAsync(userId);
            await UpdateTokensRevokedAtAsync(userId);
            await _dbContext.SaveChangesAsync();

            Log.Information("Tüm token'lar revoke edildi. UserId: {UserId}", userId);
        }

        // ========== PRIVATE HELPERS ==========

        private (string token, DateTime expires) GenerateAccessToken(IdentityUser user, IList<string> roles)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Name, user.UserName),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
            };

            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["JwtSettings:SecretKey"]));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var expirationMinutes = double.Parse(_configuration["JwtSettings:ExpirationInMinutes"]);
            var expires = DateTime.UtcNow.AddMinutes(expirationMinutes);

            var token = new JwtSecurityToken(
                issuer: _configuration["JwtSettings:Issuer"],
                audience: _configuration["JwtSettings:Audience"],
                claims: claims,
                expires: expires,
                signingCredentials: creds
            );

            return (new JwtSecurityTokenHandler().WriteToken(token), expires);
        }

        private static string GenerateRefreshTokenString()
        {
            var randomNumber = new byte[64];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomNumber);
            return Convert.ToBase64String(randomNumber);
        }

        /// <summary>
        /// Expired JWT'den principal bilgisini çıkarır (Lifetime validation kapalı).
        /// </summary>
        private ClaimsPrincipal? GetPrincipalFromExpiredToken(string token)
        {
            try
            {
                var tokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = false, // Expired token'ı kabul et
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = _configuration["JwtSettings:Issuer"],
                    ValidAudience = _configuration["JwtSettings:Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(_configuration["JwtSettings:SecretKey"]))
                };

                var tokenHandler = new JwtSecurityTokenHandler();
                var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out var securityToken);

                if (securityToken is not JwtSecurityToken jwtSecurityToken ||
                    !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
                {
                    return null;
                }

                return principal;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Kullanıcının tüm aktif refresh token'larını revoke eder (SaveChanges çağırmaz).
        /// </summary>
        private async Task RevokeAllUserRefreshTokensInternalAsync(string userId)
        {
            var activeTokens = await _dbContext.UserRefreshTokens
                .Where(t => t.UserId == userId && t.RevokedAt == null)
                .ToListAsync();

            foreach (var token in activeTokens)
            {
                token.RevokedAt = DateTime.UtcNow;
            }
        }

        /// <summary>
        /// USER_TOKEN_SECURITY tablosunda kullanıcının TokensRevokedAt değerini günceller (UPSERT).
        /// Bu, eski JWT'lerin OnTokenValidated event'inde anında reddedilmesini sağlar.
        /// SaveChanges çağırmaz — caller'a bırakır.
        /// </summary>
        private async Task UpdateTokensRevokedAtAsync(string userId)
        {
            // TokensRevokedAt'ı saniye hassasiyetine truncate et.
            // JWT iat claim'i Unix saniye olduğu için milisaniye farkı
            // yeni üretilen token'ın anında reddedilmesine neden olur.
            var nowTruncated = DateTimeOffset.FromUnixTimeSeconds(
                DateTimeOffset.UtcNow.ToUnixTimeSeconds()).UtcDateTime;

            var security = await _dbContext.UserTokenSecurities
                .FirstOrDefaultAsync(s => s.UserId == userId);

            if (security == null)
            {
                security = new UserTokenSecurity
                {
                    UserId = userId,
                    TokensRevokedAt = nowTruncated,
                    UpdatedAt = DateTime.UtcNow
                };
                _dbContext.UserTokenSecurities.Add(security);
            }
            else
            {
                security.TokensRevokedAt = nowTruncated;
                security.UpdatedAt = DateTime.UtcNow;
            }
        }

        private int GetRefreshTokenExpirationDays()
        {
            // Support both config key names for backwards compatibility
            var days = _configuration["JwtSettings:RefreshTokenExpirationDays"]
                    ?? _configuration["JwtSettings:RefreshTokenExpirationInDays"];
            return int.TryParse(days, out var result) ? result : 7; // Default: 7 gün
        }
    }
}
