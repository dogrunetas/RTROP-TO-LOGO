namespace RTROPToLogoIntegration.Domain.Entities
{
    /// <summary>
    /// Stok Ambar Tanımları (InvDef)
    /// </summary>
    public class InvDef
    {
        public int LogicalRef { get; set; }
        public int ItemRef { get; set; }
        public double MinLevel { get; set; }
        public double MaxLevel { get; set; }
        public double SafeLevel { get; set; }
        public int InvenNo { get; set; }
    }
}
