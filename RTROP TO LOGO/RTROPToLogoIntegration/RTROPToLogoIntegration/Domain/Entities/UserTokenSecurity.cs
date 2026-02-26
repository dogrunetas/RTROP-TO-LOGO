using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RTROPToLogoIntegration.Domain.Entities
{
    /// <summary>
    /// Kullanıcı bazlı token güvenlik bilgisi.
    /// TokensRevokedAt değeri, bu tarihten önce üretilmiş TÜM JWT'leri geçersiz kılar.
    /// </summary>
    [Table("USER_TOKEN_SECURITY")]
    public class UserTokenSecurity
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(450)]
        public string UserId { get; set; }

        /// <summary>
        /// Bu tarihten önce üretilmiş JWT'ler geçersizdir.
        /// Login veya revoke olduğunda güncellenir.
        /// </summary>
        [Required]
        public DateTime TokensRevokedAt { get; set; }

        public DateTime UpdatedAt { get; set; }
    }
}
