namespace RTROPToLogoIntegration.Domain.Entities
{
    /// <summary>
    /// Logo Stok Kartı (Items)
    /// </summary>
    public class Item
    {
        public int LogicalRef { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public string SpeCode2 { get; set; } // MTO/MTS alanı
        public string CardType { get; set; }
    }
}
