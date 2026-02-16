using RTROPToLogoIntegration.Application.DTOs;
using RTROPToLogoIntegration.Domain.Models;
using RTROPToLogoIntegration.Infrastructure.Persistence;
using RTROPToLogoIntegration.Infrastructure.Services;
using Serilog;

namespace RTROPToLogoIntegration.Application.Features.MRP.Commands
{
    public class ProcessMrpCommand
    {
        public List<MrpRawItemDto> Items { get; set; }
        public string FirmNo { get; set; }
        public string PeriodNr { get; set; }
    }

    public class ProcessMrpCommandHandler
    {
        private readonly StockRepository _stockRepository;
        private readonly LogoClientService _logoClientService;

        public ProcessMrpCommandHandler(StockRepository stockRepository, LogoClientService logoClientService)
        {
            _stockRepository = stockRepository;
            _logoClientService = logoClientService;
        }

        public async Task<bool> Handle(ProcessMrpCommand request)
        {
            // 1. Fiş Numarasını Üret (Form1.cs mantığı)
            var ficheNo = await _stockRepository.GetLastMRPNumberAsync(request.FirmNo, request.PeriodNr);

            var mrpList = new LogoMRPModels
            {
                FICHENO = ficheNo,
                NUMBER = ficheNo, // Form1.cs bunu da eşitliyor
                DATE = DateTime.Now,
                TRANSACTIONS = new Transactions { items = new List<TransactionItem>() }
            };

            int updatedCount = 0;

            foreach (var item in request.Items)
            {
                // 1. Ref, UnitCode ve CardType Çek (Db'den her zaman taze veri)
                var (itemRef, unitCode, cardType) = await _stockRepository.GetItemRefAndUnitByCodeAsync(item.ItemID, request.FirmNo);
                
                if (itemRef == 0)
                {
                    Log.Warning($"Malzeme bulunamadı: {item.ItemID}");
                    continue;
                }

                // 2. Stok ve OpenPO Çek (Form1.cs sorguları ile)
                var stockQty = await _stockRepository.GetStockQuantityAsync(itemRef, request.FirmNo, request.PeriodNr);
                var openPo = await _stockRepository.GetOpenPoQuantityAsync(itemRef, request.FirmNo, request.PeriodNr);

                // 3. Hesaplama (Form1.cs mantığı)
                // Form1.cs Formülü: Need = IncomingAmount - (ROP - Stock - OpenPO)
                // Burada item.ROP_update_OrderQuantity -> Incoming Amount
                
                var netStock = stockQty + openPo; 
                var ropGap = item.ROP - netStock; 
                
                double need = item.ROP_update_OrderQuantity - ropGap;
                
                // 4. Karar ve Güncelleme
                if (need > 0 && item.PlanningType == "MTS")
                {
                    // Logo'da SPECODE2 (MTS olarak) güncelle
                    await _stockRepository.UpdateItemSpeCode2Async(itemRef, "MTS", request.FirmNo);
                    
                    // 1. ABC Kodunu Hesapla (Form1.cs mantığı)
                    string abcRaw = item.ROP_update_ABCDClassification?.ToUpper() ?? "";
                    int _abcCode = 0;
                    if (abcRaw == "A") _abcCode = 1;
                    else if (abcRaw == "B") _abcCode = 2;
                    else if (abcRaw == "C") _abcCode = 3;
                    else if (abcRaw == "-") _abcCode = 0;

                    // 2. Tek seferde INVDEF güncelle (ABC + Seviyeler)
                    await _stockRepository.UpdateInvDefAsync(
                        itemRef, 
                        item.ROP,       // MinLevel
                        item.Max,       // MaxLevel
                        item.SafetyStock, // SafeLevel
                        _abcCode,       // AbcCode
                        request.FirmNo
                    );
                    
                    updatedCount++;

                    // 5. Transaction Item Oluştur
                    var transItem = new TransactionItem
                    {
                        ITEMREF = itemRef,
                        AMOUNT = need,
                        UNIT_CODE = unitCode, 
                        MRP_HEAD_TYPE = 2, // Varsayılan değer
                        STATUS = 1 
                    };

                    // Form1.cs logic'ine uygun MEET_TYPE Belirleme
                    // User Request: Excel'den ItemType okunmayacak, biz bulacağız.
                    // CardType: 10(Hammadde), 11(Yarı Mamul), 12(Mamul)
                    
                    if (cardType == 11 || cardType == 12)
                    {
                        // Üretim (Production)
                        transItem.MEET_TYPE = 1; 
                        transItem.BOMMASTERREF = item.BomMasterRef;
                        transItem.BOMREVREF = item.BomRevRef;
                    }
                    else
                    {
                        // Satınalma (Purchasing) - Hammadde(10), Ticari Mal(1) vb.
                        transItem.MEET_TYPE = 0; 
                        transItem.CLIENTREF = item.ClientRef;
                    }

                    // Ambar (Warehouse) hakkında:
                    // Kullanıcı "Ambar... bizim tarafımızdan doldurulacak" dedi ancak "Logo'ya post edilmeyecek" dedi.
                    // Bu nedenle TransactionItem içine eklemedik. Header'da da varsayılan değerler geçerli olabilir.

                    mrpList.TRANSACTIONS.items.Add(transItem);
                }
            }

            // Logo Gönderimi
            if (mrpList.TRANSACTIONS.items.Count > 0)
            {
                try 
                {
                    await _logoClientService.PostDemandFicheAsync(mrpList, request.FirmNo);
                    Log.Information("MRP İşlemi Tamamlandı. {Count} adet ürün işlendi. Fiş No: {FicheNo}", mrpList.TRANSACTIONS.items.Count, mrpList.FICHENO);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Logo gönderimi başarısız. Fiş No: {FicheNo}", mrpList.FICHENO);
                    throw; // Controller yakalayacak
                }
            }
            else
            {
                Log.Information("MRP İşlemi: İşlenecek veya ihtiyaç duyulan MTS kaydı bulunamadı.");
            }

            return true;
        }
    }
}
