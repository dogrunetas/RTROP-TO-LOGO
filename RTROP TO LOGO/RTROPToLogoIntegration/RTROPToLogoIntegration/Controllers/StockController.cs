using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RTROPToLogoIntegration.Infrastructure.Persistence;

namespace RTROPToLogoIntegration.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class StockController : ControllerBase
    {
        private readonly StockRepository _stockRepository;
        private readonly IConfiguration _configuration;

        public StockController(StockRepository stockRepository, IConfiguration configuration)
        {
            _stockRepository = stockRepository;
            _configuration = configuration;
        }

        /// <summary>
        /// Gelecek mal ve eldeki stok miktarını getirir.
        /// </summary>
        /// <param name="itemRef">Stok Kartı LogicalRef</param>
        /// <returns>Stok durumu</returns>
        [HttpGet("status/{itemRef}")]
        public async Task<IActionResult> GetStockStatus(int itemRef)
        {
            // Firma Numarası Header'dan gelir (Middleware kontrol etti)
            if (!Request.Headers.TryGetValue("x-firm-no", out var firmNoHeader))
            {
                // Middleware zaten 400 döndü ama garanti olsun
                return BadRequest("Firma numarası eksik.");
            }
            string firmNo = firmNoHeader.ToString();

            // Dönem bilgisi genelde config'den veya request'ten alınır.
            // Örnekte appsettings'den alıyoruz. İsterse header veya parametre olarak da alınabilir.
            string periodNo = _configuration["Logo:PeriodNumber"] ?? "01";

            var openPo = await _stockRepository.GetOpenPoQuantityAsync(itemRef, firmNo, periodNo);
            var onHand = await _stockRepository.GetStockQuantityAsync(itemRef, firmNo, periodNo);

            return Ok(new
            {
                ItemRef = itemRef,
                OpenPO = openPo,
                OnHand = onHand,
                TotalAvailable = openPo + onHand
            });
        }
    }
}
