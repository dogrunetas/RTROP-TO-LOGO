using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RTROPToLogoIntegration.Domain.Entities
{
    /// <summary>
    /// Kullanıcıya ait refresh token kayıtlarını tutar.
    /// Her login veya refresh işleminde eski token invalidate edilir, yenisi oluşturulur (Token Rotation).
    /// </summary>
    [Table("USER_REFRESH_TOKENS")]
    public class UserRefreshToken
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>ASP.NET Identity User ID</summary>
        [Required]
        [MaxLength(450)]
        public string UserId { get; set; }

        /// <summary>Refresh token değeri (Base64 encoded random bytes)</summary>
        [Required]
        [MaxLength(256)]
        public string Token { get; set; }

        /// <summary>Token son kullanma tarihi</summary>
        public DateTime ExpiresAt { get; set; }

        /// <summary>Token oluşturulma tarihi</summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Token kullanılarak yenilendiğinde bu tarih set edilir</summary>
        public DateTime? RevokedAt { get; set; }

        /// <summary>Bu token kullanılarak oluşturulan yeni token'ın değeri</summary>
        [MaxLength(256)]
        public string? ReplacedByToken { get; set; }

        /// <summary>İstemci IP adresi (audit için)</summary>
        [MaxLength(50)]
        public string? CreatedByIp { get; set; }

        /// <summary>Token hâlâ aktif mi?</summary>
        [NotMapped]
        public bool IsActive => RevokedAt == null && DateTime.UtcNow < ExpiresAt;

        /// <summary>Token süresi dolmuş mu?</summary>
        [NotMapped]
        public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    }
}
