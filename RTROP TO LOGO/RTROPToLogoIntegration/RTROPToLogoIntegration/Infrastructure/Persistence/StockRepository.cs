using Dapper;
using Microsoft.Data.SqlClient;
using System.Data;

namespace RTROPToLogoIntegration.Infrastructure.Persistence
{
    /// <summary>
    /// Logo veritabanı stok işlemleri (Dapper).
    /// </summary>
    public class StockRepository
    {
        private readonly IConfiguration _configuration;

        public StockRepository(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        private string GetConnectionString()
        {
            return _configuration.GetConnectionString("LogoConnection");
        }

        // Helper to manage connection lifecycle
        private async Task<T> ExecuteWithConnectionAsync<T>(Func<IDbConnection, Task<T>> action, IDbConnection? externalConnection)
        {
            if (externalConnection != null)
            {
                return await action(externalConnection);
            }

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                return await action(connection);
            }
        }

        /// <summary>
        /// Gelecek Mal (Açık Sipariş) miktarını getirir.
        /// Kaynak: Form1.cs (Legacy Logic)
        /// </summary>
        public async Task<double> GetOpenPoQuantityAsync(int itemRef, string firmNo, string periodNr, IDbConnection? connection = null, IDbTransaction? transaction = null)
        {
            // Form1.cs'ten alınan orijinal sorgu
            // Tablo isimleri parametrik yapıldı.
            string sql = $@"
                SELECT SUM(ORFL.AMOUNT - ORFL.SHIPPEDAMOUNT) AS TOTAL
                FROM LG_{firmNo}_{periodNr}_ORFLINE ORFL
                INNER JOIN LG_{firmNo}_{periodNr}_ORFICHE ORF ON ORF.LOGICALREF = ORFL.ORDFICHEREF
                INNER JOIN LG_{firmNo}_ITEMS ITM ON ITM.LOGICALREF = ORFL.STOCKREF
                WHERE ORF.TRCODE=2 AND ORFL.TRCODE=2 AND ITM.ACTIVE=0
                AND ORF.CANCELLED=0 AND ORFL.CANCELLED=0 AND ORFL.CLOSED=0
                AND (ORFL.AMOUNT - ORFL.SHIPPEDAMOUNT) > 0
                AND ORF.STATUS=4 AND ITM.LOGICALREF=@ItemRef";

            return await ExecuteWithConnectionAsync(async conn =>
            {
                var result = await conn.ExecuteScalarAsync<object>(sql, new { ItemRef = itemRef }, transaction: transaction);
                return Convert.ToDouble(result ?? 0);
            }, connection);
        }

        /// <summary>
        /// Malzeme Kodu'ndan (ItemID) LOGICALREF getirir.
        /// </summary>
        public async Task<int> GetItemRefByCodeAsync(string itemCode, string firmNo, IDbConnection? connection = null, IDbTransaction? transaction = null)
        {
            string sql = $@"
                SELECT LOGICALREF
                FROM LG_{firmNo}_ITEMS WITH(NOLOCK)
                WHERE CODE = @Code";

            return await ExecuteWithConnectionAsync(async conn =>
            {
                var result = await conn.ExecuteScalarAsync<int?>(sql, new { Code = itemCode }, transaction: transaction);
                return result ?? 0;
            }, connection);
        }

        /// <summary>
        /// Eldeki Stok (On Hand) miktarını getirir.
        /// Kaynak: Form1.cs (Legacy Logic - GNTOTST)
        /// </summary>
        public async Task<double> GetStockQuantityAsync(int itemRef, string firmNo, string periodNr, IDbConnection? connection = null, IDbTransaction? transaction = null)
        {
            // Form1.cs'ten alınan sorgu.
            // NOT: GNTOTST tablosu/view'ı dönem eki alır: LV_{firm}_{period}_GNTOTST
            string sql = $@"
                SELECT SUM(ISNULL(ONHAND,0)) as Total
                FROM LV_{firmNo}_{periodNr}_GNTOTST
                WHERE STOCKREF=@ItemRef AND INVENNO NOT IN (-1,5,6,9,10,11,12)";

            return await ExecuteWithConnectionAsync(async conn =>
            {
                var result = await conn.ExecuteScalarAsync<object>(sql, new { ItemRef = itemRef }, transaction: transaction);
                return Convert.ToDouble(result ?? 0);
            }, connection);
        }

        /// <summary>
        /// Stok kartının Özel Kod 2 (SpeCode2) alanını günceller.
        /// Kaynak: Form1.cs
        /// </summary>
        public async Task UpdateItemSpeCode2Async(int itemRef, string speCode2, string firmNo, IDbConnection? connection = null, IDbTransaction? transaction = null)
        {
            string sql = $@"
                UPDATE LG_{firmNo}_ITEMS
                SET SPECODE2=@SpeCode2
                WHERE LOGICALREF=@ItemRef";

            await ExecuteWithConnectionAsync(async conn =>
            {
                await conn.ExecuteAsync(sql, new { ItemRef = itemRef, SpeCode2 = speCode2 }, transaction: transaction);
                return 0; // Dummy return
            }, connection);
        }

        /// <summary>
        /// Form1.cs -> UpdateINVDef metodunun birebir karşılığıdır.
        /// ABC Kodu, Min, Max ve Güvenlik stoğunu INVDEF tablosunda (INVENNO=0) günceller.
        /// </summary>
        public async Task UpdateInvDefAsync(int itemRef, double minLevel, double maxLevel, double safeLevel, int abcCode, string firmNo, int invenNo, IDbConnection? connection = null, IDbTransaction? transaction = null)
        {
            // Form1.cs Satır 392 referans alınmıştır. (INVENNO dinamik oldu)
            string sql = $@"
                UPDATE LG_{firmNo}_INVDEF 
                SET SAFELEVEL=@SafeLevel, ABCCODE=@AbcCode, MINLEVEL=@MinLevel, MAXLEVEL=@MaxLevel 
                WHERE INVENNO=@InvenNo AND ITEMREF=@ItemRef";

            await ExecuteWithConnectionAsync(async conn =>
            {
                await conn.ExecuteAsync(sql, new
                {
                    ItemRef = itemRef,
                    MinLevel = minLevel,
                    MaxLevel = maxLevel,
                    SafeLevel = safeLevel,
                    AbcCode = abcCode,
                    InvenNo = invenNo
                }, transaction: transaction);
                return 0;
            }, connection);
        }

        /// <summary>
        /// Malzeme Kodu'ndan LOGICALREF, Ana Birim Kodu (UNITCODE) ve Kart Tipi (CARDTYPE) getirir.
        /// Kaynak: Form1.cs (GetItemRef mantığı + UnitSetL join)
        /// </summary>
        public async Task<(int ItemRef, string UnitCode, int CardType)> GetItemRefAndUnitByCodeAsync(string itemCode, string firmNo, IDbConnection? connection = null, IDbTransaction? transaction = null)
        {
            // Items tablosu UnitSetRef tutar. UnitSetL (Line) tablosunda MainUnit=1 olan satır ana birimdir.
            string sql = $@"
                SELECT I.LOGICALREF, U.CODE, I.CARDTYPE
                FROM LG_{firmNo}_ITEMS I WITH(NOLOCK)
                LEFT JOIN LG_{firmNo}_UNITSETL U WITH(NOLOCK) ON U.UNITSETREF = I.UNITSETREF AND U.MAINUNIT = 1
                WHERE I.CODE = @Code";

            return await ExecuteWithConnectionAsync(async conn =>
            {
                var result = await conn.QueryFirstOrDefaultAsync(sql, new { Code = itemCode }, transaction: transaction);
                
                if (result == null) return (0, "", 0);
                return ((int)result.LOGICALREF, (string)result.CODE, (int)result.CARDTYPE);
            }, connection);
        }

        /// <summary>
        /// Son MRP Fiş Numarasını getirir veya yenisini üretir.
        /// Format: MRPYYYYMM-XXXXX (Örn: MRP202502-00001)
        /// Kaynak: Form1.cs -> GetLastMRPNumber
        /// </summary>
        public async Task<string> GetLastMRPNumberAsync(string firmNo, string periodNr, IDbConnection? connection = null, IDbTransaction? transaction = null)
        {
            // Form1.cs'deki mantık: MRP + YYYY + MM + -
            string mrpHeader = "MRP";
            string year = DateTime.Now.ToString("yyyy");
            string month = DateTime.Now.ToString("MM");
            string searchPattern = $"{mrpHeader}{year}{month}-";

            // Form1.cs'deki SQL Sorgusunun Birebir Aynısı (Dapper ile)
            // LG_{firm}_{period}_DEMANDFICHE tablosuna bakılır.
            string sql = $@"
                SELECT TOP 1 
                    CONCAT(
                        LEFT(FICHENO, 11), 
                        RIGHT('00000' + CAST(CAST(RIGHT(FICHENO, 5) AS INT) + 1 AS VARCHAR), 5)
                    ) AS YENI_FICHENO
                FROM LG_{firmNo}_{periodNr}_DEMANDFICHE
                WHERE FICHENO LIKE @SearchPattern + '%' 
                ORDER BY FICHENO DESC";

            return await ExecuteWithConnectionAsync(async conn =>
            {
                var newFicheNo = await conn.ExecuteScalarAsync<string>(sql, new { SearchPattern = searchPattern }, transaction: transaction);

                if (string.IsNullOrEmpty(newFicheNo))
                {
                    // İlk kayıt: MRP202502-00001
                    return $"{searchPattern}000001";
                }

                return newFicheNo;
            }, connection);
        }
        /// <summary>
        /// Ürünün tipini (MAMÜL=12, YARI=11, HAM=10) bulur.
        /// </summary>
        public async Task<string> GetCardTypeAsync(string itemCode, string firmNo, IDbConnection? connection = null, IDbTransaction? transaction = null)
        {
            string sql = $"SELECT CARDTYPE FROM LG_{firmNo}_ITEMS WITH(NOLOCK) WHERE CODE=@Code";
            return await ExecuteWithConnectionAsync(async conn =>
            {
                var result = await conn.ExecuteScalarAsync<object>(sql, new { Code = itemCode }, transaction: transaction);
                return result?.ToString() ?? "";
            }, connection);
        }

        /// <summary>
        /// Ürünün BOMMASTERREF ve VALIDREVREF bilgilerini bulur (Form1.cs -> GetBomref).
        /// </summary>
        public async Task<(int BomRef, int BomRevRef)> GetBomInfoAsync(int itemRef, string firmNo, IDbConnection? connection = null, IDbTransaction? transaction = null)
        {
            string sql = $@"SELECT TOP 1 LOGICALREF, VALIDREVREF FROM LG_{firmNo}_BOMASTER WITH(NOLOCK) WHERE MAINPRODREF=@ItemRef ORDER BY LOGICALREF DESC";
            
            return await ExecuteWithConnectionAsync(async conn =>
            {
                var result = await conn.QueryFirstOrDefaultAsync(sql, new { ItemRef = itemRef }, transaction: transaction);

                if (result == null) return (0, 0);
                return ((int)result.LOGICALREF, (int)result.VALIDREVREF);
            }, connection);
        }

        /// <summary>
        /// Hammadde ise tedarikçisini (CLIENTREF) bulur (Form1.cs -> GetClientRef).
        /// </summary>
        public async Task<int> GetClientRefAsync(int itemRef, string firmNo, IDbConnection? connection = null, IDbTransaction? transaction = null)
        {
            string sql = $@"SELECT TOP 1 CLIENTREF FROM LG_{firmNo}_SUPPASGN WITH(NOLOCK) WHERE ITEMREF=@ItemRef AND CLCARDTYPE=1 ORDER BY PRIORITY ASC";

            return await ExecuteWithConnectionAsync(async conn =>
            {
                var result = await conn.ExecuteScalarAsync<int?>(sql, new { ItemRef = itemRef }, transaction: transaction);
                return result ?? 0;
            }, connection);
        }
    }
}
