using Microsoft.AspNetCore.Identity;
using RTROPToLogoIntegration.Domain.Models;

namespace RTROPToLogoIntegration.Infrastructure.Identity
{
    public interface ITokenService
    {
        /// <summary>
        /// Yeni access token + refresh token üretir ve DB'ye kaydeder.
        /// Aynı zamanda kullanıcının önceki tüm refresh token'larını revoke eder.
        /// </summary>
        Task<AuthResponse> GenerateTokenAsync(IdentityUser user, string? ipAddress = null);

        /// <summary>
        /// Expired access token + geçerli refresh token ile yeni token çifti üretir.
        /// Token rotation uygular (eski refresh token invalidate, yeni üretilir).
        /// Stolen token algılarsa tüm token ailesini revoke eder.
        /// </summary>
        Task<AuthResponse?> RefreshTokenAsync(string accessToken, string refreshToken, string? ipAddress = null);

        /// <summary>
        /// Kullanıcının TÜM aktif refresh token'larını revoke eder.
        /// Aynı zamanda TokensRevokedAt'ı günceller (eski JWT'leri anında geçersiz kılar).
        /// </summary>
        Task RevokeAllTokensAsync(string userId);
    }
}
