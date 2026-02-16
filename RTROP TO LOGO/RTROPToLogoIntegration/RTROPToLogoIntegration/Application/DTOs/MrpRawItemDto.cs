namespace RTROPToLogoIntegration.Application.DTOs
{
    public class MrpRawItemDto
    {
        public string ItemID { get; set; } // Excel: ItemID (Malzeme Kodu)
        public string ROP_update_ABCDClassification { get; set; } // Excel: ROP_update_ABCDClassification
        public string PlanningType { get; set; } // Excel: "Planning Type" (Boşluksuz)
        public double SafetyStock { get; set; } // Excel: SafetyStock (SafeLevel)
        public double ROP { get; set; } // Excel: ROP (MinLevel / Reorder Point)
        public double Max { get; set; } // Excel: Max (MaxLevel)
        public double ROP_update_OrderQuantity { get; set; } // Excel: ROP_update_OrderQuantity (Incoming Amount)
        public int ItemRef { get; set; } // Excel: ItemRef (Grid'den)
        public int BomMasterRef { get; set; } // Excel: BOMREF
        public int BomRevRef { get; set; } // Excel: BOMREVREF
        public int ClientRef { get; set; } // Excel: CLIENTREF
        public string? ItemType { get; set; } // Excel: Ürün Tipi (Kullanılmıyor - DB'den CardType bakılıyor)
        public string? Ambar { get; set; } // Excel: Ambar (Kullanılmıyor)
    }
}
