namespace RTROPToLogoIntegration.Domain.Entities
{
    /// <summary>
    /// Talep Fişi (MRP Sonucu)
    /// </summary>
    public class DemandFiche
    {
        public string FicheNo { get; set; }
        public DateTime Date { get; set; }
        public string Time { get; set; }
        public int Status { get; set; }
        
        // Talep Satırları (DemandLines)
        // Not: Kullanıcı şimdilik detay vermediği için object veya boş liste bırakılabilir
        // ancak Entity olarak istendiği için generic bir list ekliyorum.
        public List<object> DemandLines { get; set; } = new List<object>();
    }
}
