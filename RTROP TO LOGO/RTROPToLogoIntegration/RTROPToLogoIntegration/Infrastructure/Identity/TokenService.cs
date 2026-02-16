using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using RTROPToLogoIntegration.Domain.Models;
using System.Security.Cryptography;

namespace RTROPToLogoIntegration.Infrastructure.Identity
{
    /// <summary>
    /// Kullanıcılar için JWT ve Refresh Token üreten servis.
    /// </summary>
    public class TokenService : ITokenService
    {
        private readonly IConfiguration _configuration;
        private readonly UserManager<IdentityUser> _userManager;

        public TokenService(IConfiguration configuration, UserManager<IdentityUser> userManager)
        {
            _configuration = configuration;
            _userManager = userManager;
        }

        public async Task<AuthResponse> GenerateTokenAsync(IdentityUser user)
        {
            // 1. Claim'lerin oluşturulması
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id), // UserId
                new Claim(ClaimTypes.Name, user.UserName),     // UserName
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var roles = await _userManager.GetRolesAsync(user);
            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            // 2. Token imzası ve süresi
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["JwtSettings:SecretKey"]));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            
            var expirationMinutes = double.Parse(_configuration["JwtSettings:ExpirationInMinutes"]);
            var expires = DateTime.Now.AddMinutes(expirationMinutes);

            var token = new JwtSecurityToken(
                issuer: _configuration["JwtSettings:Issuer"],
                audience: _configuration["JwtSettings:Audience"],
                claims: claims,
                expires: expires,
                signingCredentials: creds
            );
            
            var accessToken = new JwtSecurityTokenHandler().WriteToken(token);

            // 3. Refresh Token üretimi
            var refreshToken = GenerateRefreshToken();

            // Opsiyonel: Refresh Token'ı veritabanına kaydedebiliriz (UserRefreshToken tablosu gibi).
            // Şu an sadece dönüyoruz.

            return new AuthResponse
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                Expiration = expires
            };
        }

        private string GenerateRefreshToken()
        {
            var randomNumber = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(randomNumber);
                return Convert.ToBase64String(randomNumber);
            }
        }
    }
}
