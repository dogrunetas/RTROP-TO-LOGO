using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RTROPToLogoIntegration.Domain.Entities
{
    /// <summary>
    /// Kullanıcıya ait refresh token kayıtlarını tutar.
    /// Token rotation ve stolen token detection mekanizması sağlar.
    /// </summary>
    [Table("USER_REFRESH_TOKENS")]
    public class UserRefreshToken
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(450)]
        public string UserId { get; set; }

        [Required]
        [MaxLength(512)]
        public string Token { get; set; }

        [Required]
        public DateTime ExpiresAt { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Token kullanıldıysa (rotate edildiyse), bu tarih set edilir.
        /// </summary>
        public DateTime? RevokedAt { get; set; }

        /// <summary>
        /// Bu token rotate edildikten sonra yerine oluşturulan yeni token.
        /// Stolen token detection için kullanılır.
        /// </summary>
        [MaxLength(512)]
        public string? ReplacedByToken { get; set; }

        /// <summary>
        /// Token'ın oluşturulduğu IP adresi. Güvenlik denetimi için.
        /// </summary>
        [MaxLength(50)]
        public string? CreatedByIp { get; set; }

        /// <summary>
        /// Token henüz aktif mi? (Revoke edilmemiş ve süresi dolmamış)
        /// </summary>
        [NotMapped]
        public bool IsActive => RevokedAt == null && !IsExpired;

        [NotMapped]
        public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    }
}
