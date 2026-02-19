using Microsoft.AspNetCore.Identity;
using RTROPToLogoIntegration.Domain.Models;

namespace RTROPToLogoIntegration.Infrastructure.Identity
{
    public interface ITokenService
    {
        /// <summary>Login sırasında access + refresh token üretir ve refresh token'ı DB'ye kaydeder.</summary>
        Task<AuthResponse> GenerateTokenAsync(IdentityUser user, string? ipAddress = null);

        /// <summary>Mevcut refresh token ile yeni access + refresh token üretir (Token Rotation).</summary>
        Task<AuthResponse?> RefreshTokenAsync(string accessToken, string refreshToken, string? ipAddress = null);

        /// <summary>Kullanıcının tüm aktif refresh token'larını iptal eder (Logout / güvenlik).</summary>
        Task RevokeAllTokensAsync(string userId);
    }
}
