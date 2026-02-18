using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RTROPToLogoIntegration.Domain.Entities
{
    [Table("LOG_INCOMING_REQUESTS")]
    public class LogIncomingRequest
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        [Required]
        [MaxLength(26)]
        public string TransactionId { get; set; } // ULID

        [MaxLength(200)]
        public string Endpoint { get; set; }

        [MaxLength(10)]
        public string Method { get; set; }

        public string RequestBody { get; set; }

        [MaxLength(50)]
        public string ClientIp { get; set; }

        [MaxLength(450)]
        public string? UserId { get; set; } // Nullable, as requests might be unauthenticated

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
