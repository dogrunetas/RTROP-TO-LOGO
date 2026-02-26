using System.ComponentModel.DataAnnotations;

namespace RTROPToLogoIntegration.Application.DTOs
{
    /// <summary>
    /// Refresh token isteği için DTO.
    /// Expired access token + geçerli refresh token gönderilir.
    /// </summary>
    public class RefreshTokenRequestDto
    {
        [Required]
        public string AccessToken { get; set; }

        [Required]
        public string RefreshToken { get; set; }
    }
}
