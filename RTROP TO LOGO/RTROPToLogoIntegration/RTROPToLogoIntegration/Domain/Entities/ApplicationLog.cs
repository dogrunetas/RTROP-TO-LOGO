using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RTROPToLogoIntegration.Domain.Entities
{
    [Table("Logs")] // Serilog'un yazdığı tablo adı
    public class ApplicationLog
    {
        [Key]
        public int Id { get; set; }

        public string? Message { get; set; }
        public string? MessageTemplate { get; set; }
        
        [MaxLength(128)]
        public string? Level { get; set; }
        
        public DateTime? TimeStamp { get; set; }
        public string? Exception { get; set; }
        public string? Properties { get; set; } // XML veya JSON tutar

        // --- BİZİM EKLEDİKLERİMİZ ---
        [MaxLength(50)]
        public string? TransactionId { get; set; } // Indexlenmesi iyi olur

        [MaxLength(100)]
        public string? UserId { get; set; }
    }
}
