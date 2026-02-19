using System.ComponentModel.DataAnnotations;

namespace RTROPToLogoIntegration.Application.DTOs
{
    /// <summary>
    /// Refresh token yenileme isteği modeli.
    /// </summary>
    public class RefreshTokenRequestDto
    {
        [Required(ErrorMessage = "AccessToken zorunludur.")]
        public string AccessToken { get; set; }

        [Required(ErrorMessage = "RefreshToken zorunludur.")]
        public string RefreshToken { get; set; }
    }
}
