using DevExpress.Utils.Extensions;
using DevExpress.XtraGrid;
using DevExpress.XtraReports.Parameters;
using OfficeOpenXml;
using OfficeOpenXml.FormulaParsing.Excel.Functions.Logical;
using RestSharp;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.OleDb;
using System.Data.SqlClient;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using TigerStockLevelManager.Exceptions;
using TigerStockLevelManager.Models;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Header;
using ParameterType = RestSharp.ParameterType;
namespace TigerStockLevelManager
{
    public partial class Form1 : Form
    {
        public int transferredRecordCount = 0;
        public Form1()
        {
            InitializeComponent();
        }

        private void readExcelFileButton_Click(object sender, EventArgs e)
        {

            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Excel Files|*.xls;*.xlsx;*.xlsm";
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                if (splashScreenManager1.IsSplashFormVisible == false)
                    splashScreenManager1.ShowWaitForm();

                transferredRecordCount = 0;
                string filePath = openFileDialog.FileName;

                label1.Text = openFileDialog.SafeFileName;

                try
                {
                    ExcelOkuVeGrideYukle(filePath);

                    if (splashScreenManager1.IsSplashFormVisible)
                        splashScreenManager1.CloseWaitForm();

                    MessageBox.Show("Excel dosyası başarıyla yüklendi.");
                }
                catch (Exception ex)
                {
                    if (splashScreenManager1.IsSplashFormVisible)
                        splashScreenManager1.CloseWaitForm();
                    MessageBox.Show("Excel dosyası okunurken hata oluştu: " + ex.Message);
                }

                if(splashScreenManager1.IsSplashFormVisible)
                    splashScreenManager1.CloseWaitForm();

            }

        }
        private void ExcelOkuVeGrideYukle(string excelDosyaYolu)
        {
            //// EPPlus lisans ayarı (versiyon 5+ için gerekli)
            //ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            DataTable dt = new DataTable();

            using (ExcelPackage package = new ExcelPackage(new FileInfo(excelDosyaYolu)))
            {
                ExcelWorksheet worksheet = package.Workbook.Worksheets[1]; // İlk sayfa

                // Kolon başlıklarını ekle
                for (int col = 1; col <= worksheet.Dimension.End.Column; col++)
                {
                    dt.Columns.Add(worksheet.Cells[1, col].Value?.ToString() ?? $"Column{col}");
                }

                // Verileri ekle (2. satırdan başla, 1. satır başlık)
                for (int row = 2; row <= worksheet.Dimension.End.Row; row++)
                {
                    DataRow dr = dt.NewRow();
                    for (int col = 1; col <= worksheet.Dimension.End.Column; col++)
                    {
                        dr[col - 1] = worksheet.Cells[row, col].Value?.ToString() ?? string.Empty;
                    }
                    dt.Rows.Add(dr);
                }

                dt.Columns.Add("Hata", typeof(string));
                dt.Columns.Add("Ürün Tipi", typeof(string));
                dt.Columns.Add("Ambar", typeof(string));
                dt.Columns.Add("BOMREF", typeof(string));
                dt.Columns.Add("BOMREVREF", typeof(string));
                dt.Columns.Add("CLIENTREF", typeof(string));
                dt.Columns.Add("ItemRef", typeof(string));
                dt.Columns.Add("UNITCODE", typeof(string));
                dt.Columns.Add("INVTOTAL", typeof(float));
                dt.Columns.Add("OpenPO", typeof(float));
                



                foreach (DataRow row in dt.Rows)
                {
                    string itemCode = row["ItemID"].ToString();
                    if (!VerifyLogoItem(itemCode))
                    {
                        row["Hata"] = "Logo'da bulunamadı";
                    }
                    // Ürün Tipi Belirleme
                    if(row["Hata"].ToString() == "")
                        AddItemType(row,itemCode);
                    if(row["Ürün Tipi"].ToString() == "")
                    {
                        row["Hata"] = "Ürün tipi belirlenemedi";
                    }
                    
                    if(row["Ürün Tipi"].ToString()== "HAMMADDE")
                        {
                        row["Ambar"] = Program.hmWarehouse;
                    }
                    else if(row["Ürün Tipi"].ToString() == "YARI MAMÜL")
                    {
                        row["Ambar"] = Program.ymWarehouse;
                    }
                    else if(row["Ürün Tipi"].ToString() == "MAMÜL")
                    {
                        row["Ambar"] = Program.mmWarehouse;
                    }
                    var itemref = GetItemRef(itemCode,false);
                    var bomref = GetBomref(itemref,false);
                    var bomrevref = GetBomref(itemref,true);
                    var unitCode = GetItemRef(itemCode, true);

                    var clientref = GetClientRef(itemref);

                    var invTotal = GetInvTotal(itemref);
                    var openPO = GetOpenPO(itemref);

                    row["ItemRef"] = itemref;
                    row["BOMREF"] = bomref;
                    row["BOMREVREF"] = bomrevref;
                    row["CLIENTREF"] = clientref;
                    row["INVTOTAL"] = invTotal;
                    row["UNITCODE"] = unitCode;
                    row["OpenPO"] = openPO;


                }

                excelContentGridControl.DataSource = dt;

                //clientref, bomref, bomrevref kolonlarını gizle
                excelContentGridView.Columns["CLIENTREF"].Visible = false;
                excelContentGridView.Columns["BOMREF"].Visible = false;
                excelContentGridView.Columns["BOMREVREF"].Visible = false;
                excelContentGridView.Columns["ItemRef"].Visible = false;
                excelContentGridView.Columns["UNITCODE"].Visible = false;

            }
        }

        private float GetOpenPO(string itemref)
        {
            float value = 0;

            try
            {
                using (SqlConnection connection = new SqlConnection(Program.connectionString))
                {
                    connection.Open();


                    string query = $@"SELECT SUM(AMOUNT-SHIPPEDAMOUNT) AS TOTAL FROM LG_{Program.companyNumber}_{Program.periodNumber}_ORFLINE ORFL
                                INNER JOIN LG_{Program.companyNumber}_{Program.periodNumber}_ORFICHE ORF ON ORF.LOGICALREF=ORFL.ORDFICHEREF
                                INNER JOIN LG_{Program.companyNumber}_ITEMS ITM ON ITM.LOGICALREF=ORFL.STOCKREF
                                WHERE ORF.TRCODE=2 AND ORFL.TRCODE=2 AND ITM.ACTIVE=0 AND 
                                ORF.CANCELLED=0 AND ORFL.CANCELLED=0 AND ORFL.CLOSED=0 AND 
                                (ORFL.AMOUNT-ORFL.SHIPPEDAMOUNT)>0 AND ORF.STATUS=4 AND ITM.LOGICALREF=@p1";

                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@p1", itemref);
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                value = Convert.ToSingle(reader["TOTAL"]);
                            }
                            reader.Close();
                        }
                    }


                    connection.Close();
                }
                return value;
            }
            catch(Exception ex)
            {
                //sonradan loglama eklenecek
                return 0;
            }
        }

        private float GetInvTotal(string itemref)
        {
            float total = 0;
            try
            {
                using(SqlConnection connection = new SqlConnection(Program.connectionString))
                {
                    connection.Open();

                    string query = $@"SELECT SUM(ISNULL(ONHAND,0)) as Total FROM LV_{Program.companyNumber}_{Program.periodNumber}_GNTOTST WHERE STOCKREF=@p1 AND INVENNO NOT IN (-1,5,6,9,10,11,12)";
                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@p1", itemref);
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                total= Convert.ToSingle(reader["Total"]);
                            }
                            reader.Close();
                        }
                    }
                }
                return total;
            }
            catch(Exception ex)
            {
                //sonradan loglama eklenecek
                return 0;
            }

        }

        private object GetClientRef(string itemref)
        {
            try
            {
                using(SqlConnection connection = new SqlConnection(Program.connectionString))
                {
                    connection.Open();
                    string query = $@"select TOP 1 CLIENTREF from LG_{Program.companyNumber}_SUPPASGN WHERE ITEMREF =@p1 AND CLCARDTYPE=1 ORDER BY PRIORITY ASC";
                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@p1", itemref);
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                return reader["CLIENTREF"].ToString();
                            }
                            reader.Close();
                        }
                    }
                }
                return "";

            }
            catch (Exception ex)
            {
                //sonradan loglama eklenecek
                return "";
            }
        }

        private string GetBomref(string itemref,bool type)
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(Program.connectionString))
                {
                    connection.Open();
                    string query = $@"SELECT TOP 1 LOGICALREF,VALIDREVREF FROM LG_{Program.companyNumber}_BOMASTER WHERE MAINPRODREF=@ItemRef ORDER BY LOGICALREF DESC";
                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@ItemRef", itemref);
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                if (type)
                                    return reader["VALIDREVREF"].ToString();
                                else
                                    return reader["BOMMASTERREF"].ToString();
                            }
                            reader.Close();
                        }
                    }
                }
                return "";
            }
            catch (Exception ex)
            {
                //sonradan loglama eklenecek
                return "";
            }
        }

        private string GetItemRef(string itemCode,bool type)
        {
            string returnValue = "";
            try
            {
                using(SqlConnection connection = new SqlConnection(Program.connectionString))
                {   connection.Open();
                    string query = $@"SELECT ITM.LOGICALREF,UNITL.CODE FROM LG_{Program.companyNumber}_ITEMS ITM 
                                    LEFT JOIN LG_{Program.companyNumber}_UNITSETL UNITL ON UNITL.UNITSETREF=ITM.UNITSETREF AND UNITL.MAINUNIT=1
                                    WHERE ITM.CODE=@Code";
                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Code", itemCode);
                        using(SqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                if(type)
                                {
                                    returnValue = reader["CODE"].ToString();
                                }
                                else
                                {
                                    returnValue = reader["LOGICALREF"].ToString();
                                }
                            }
                            reader.Close();
                        }
                    }
                }
                return returnValue;
            }
            catch (Exception ex)
            {
                //sonradan loglama eklenecek
                return returnValue;
            }

        }

        private void AddItemType(DataRow row,string itemCode)
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(Program.connectionString))
                {
                    connection.Open();
                    string query = $@"SELECT CARDTYPE FROM LG_{Program.companyNumber}_ITEMS WHERE CODE=@Code";
                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Code", itemCode);
                        
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {   
                                string type="";
                                var cardType = reader["CARDTYPE"].ToString();
                                if (cardType == "10")
                                {
                                    type = "HAMMADDE";
                                }
                                else if (cardType == "11")
                                {
                                    type = "YARI MAMÜL";
                                }
                                else if (cardType == "12")
                                {
                                    type = "MAMÜL";
                                }
                                row["Ürün Tipi"] = type;
                            }
                            reader.Close();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                //sonradan loglama eklenecek
            }
        }

        private void ItemMTOOrMTSSelect(string code, string mtoOrMts)
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(Program.connectionString))
                {
                    connection.Open();
                    string query = $@"update LG_{Program.companyNumber}_ITEMS set SPECODE2=@p1 where CODE=@Code";
                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Code", code);
                        command.Parameters.AddWithValue("@p1", mtoOrMts);
                        command.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                //sonradan loglama eklenecek
            }
        }

        private async void transferToLogo_Click(object sender, EventArgs e)
        {
            if (splashScreenManager1.IsSplashFormVisible == false)
                splashScreenManager1.ShowWaitForm();
            for (int i = 0; i < excelContentGridView.RowCount; i++)
            {
                // Her bir satırı Logo'ya aktar
                var item = excelContentGridView.GetRowCellValue(i, "ItemID").ToString(); // Değiştirin
                var qty = Convert.ToDecimal(excelContentGridView.GetRowCellValue(i, "SafetyStock")); // Değiştirin
                var minLevel = Convert.ToDecimal(excelContentGridView.GetRowCellValue(i, "ROP")); // Değiştirin
                var maxLevel = Convert.ToDecimal(excelContentGridView.GetRowCellValue(i, "Max")); // Değiştirin
                var abcCode = excelContentGridView.GetRowCellValue(i, "ROP_update_ABCDClassification").ToString(); // Değiştirin
                var mtoOrMts = excelContentGridView.GetRowCellValue(i, "Planning Type").ToString(); // Değiştirin
                var error = excelContentGridView.GetRowCellValue(i, "Hata").ToString();
                var itemType = excelContentGridView.GetRowCellValue(i, "Ürün Tipi").ToString(); // Değiştirin
                var ambar = excelContentGridView.GetRowCellValue(i, "Ambar").ToString(); // Değiştirin

                if (error == "" && itemType != "" && ambar != "")
                {
                    TransferLogoItemSafetyStock(item, qty, minLevel, maxLevel, abcCode, ambar);
                    ItemMTOOrMTSSelect(item, mtoOrMts);
                }

            }



            if (splashScreenManager1.IsSplashFormVisible)
                splashScreenManager1.CloseWaitForm();

            MessageBox.Show($"Ürünlerin tipi (MTO, MTS) ve stok seviyeleri aktarıldı!. Toplam aktarılan kayıt sayısı: {transferredRecordCount}");

            MessageBox.Show("Üretim Planlama Önerisi ekleniyor!", "Bilgi", MessageBoxButtons.OK);

            if (splashScreenManager1.IsSplashFormVisible == false)
                splashScreenManager1.ShowWaitForm();

               await CreateMRPPlan();

            if (splashScreenManager1.IsSplashFormVisible)
                splashScreenManager1.CloseWaitForm();

        }

        private async Task CreateMRPPlan()
        {
            LogoMRPModels logoMRP = new LogoMRPModels();
            logoMRP.FICHENO = GetLastMRPNumber();
            logoMRP.NUMBER = logoMRP.FICHENO;
            //2025-11-25T00:00:00
            logoMRP.DATE= Convert.ToDateTime(DateTime.Now.ToString("yyyy-MM-dd") + "T00:00:00");
            logoMRP.TIME = Convert.ToInt64(DateTime.Now.ToString("HHmmss"));
            logoMRP.STATUS = 1;
            logoMRP.DEMANDTYPE = 0;
            logoMRP.USERNO = Convert.ToInt32(Program.LogoUserNumber);
            logoMRP.MPS_CODE = "MRP";
            logoMRP.USER_NO= Convert.ToInt32(Program.LogoUserNumber);
            logoMRP.XML_ATTRIBUTE = 1;
            logoMRP.DEMAND_TYPE = 0;
            logoMRP.TRANSACTIONS = new Transactions();
            logoMRP.TRANSACTIONS.items = new List<TransactionItem>();
            int lineCount = 0;
            for (int i = 0; i < excelContentGridView.RowCount; i++)
            {
                var item = excelContentGridView.GetRowCellValue(i, "ItemID").ToString(); // Değiştirin
                var itemRef = excelContentGridView.GetRowCellValue(i, "ItemRef").ToString(); // Değiştirin
                var bomRef = excelContentGridView.GetRowCellValue(i, "BOMREF").ToString(); // Değiştirin
                var bomRevRef = excelContentGridView.GetRowCellValue(i, "BOMREVREF").ToString(); // Değiştirin
                var clientRef = excelContentGridView.GetRowCellValue(i, "CLIENTREF").ToString(); // Değiştirin
                var amount = excelContentGridView.GetRowCellValue(i, "ROP_update_OrderQuantity").ToString(); // Değiştirin
                var itemType = excelContentGridView.GetRowCellValue(i, "Ürün Tipi").ToString(); // Değiştirin
                var error = excelContentGridView.GetRowCellValue(i, "Hata").ToString();
                var UNITCODE = excelContentGridView.GetRowCellValue(i, "UNITCODE").ToString(); // Değiştirin
                var ambar = excelContentGridView.GetRowCellValue(i, "Ambar").ToString(); // Değiştirin
                var invTotal = Convert.ToDouble(excelContentGridView.GetRowCellValue(i, "INVTOTAL").ToString()); // Değiştirin
                var rop = Convert.ToDouble(excelContentGridView.GetRowCellValue(i, "ROP").ToString()); // Değiştirin
                var openPO = Convert.ToDouble(excelContentGridView.GetRowCellValue(i, "OpenPO").ToString()); // Değiştirin
                var planningType = excelContentGridView.GetRowCellValue(i, "Planning Type").ToString(); // Değiştirin
                if (error == "" && itemType!="" && itemRef!="" && ambar!="" && Convert.ToDouble(amount) > 0 && (invTotal+openPO)<rop && planningType=="MTS" )
                {
                    lineCount++;
                    TransactionItem transactionItem = new TransactionItem();
                    transactionItem.ITEMREF = Convert.ToInt32(itemRef);
                    transactionItem.LINE_NO = lineCount;
                    transactionItem.STATUS = 1;
                    transactionItem.MRP_HEAD_TYPE = 2; 
                    transactionItem.PORDER_TYPE = 0;
                    transactionItem.BOM_TYPE = 0;
                    transactionItem.XML_ATTRIBUTE = 1;
                    transactionItem.AMOUNT = Convert.ToDouble(amount)-(rop-invTotal-openPO);
                    transactionItem.UNIT_CODE = UNITCODE;
                    transactionItem.SOURCE_INDEX = 0;

                    if (itemType=="MAMÜL" || itemType=="YARI MAMUL")
                    {
                        transactionItem.MEET_TYPE = 1;
                        if(bomRef != "" && bomRevRef != "")
                        {
                            transactionItem.BOMMASTERREF = Convert.ToInt32(bomRef);
                            transactionItem.BOMREVREF = Convert.ToInt32(bomRevRef);
                        }
                        else
                        {
                            transactionItem.BOMMASTERREF = 0;
                            transactionItem.BOMREVREF = 0;
                        }

                    }
                    else if(itemType=="HAMMADDE")
                    {
                        transactionItem.MEET_TYPE = 0;
                        if(clientRef != "")
                            transactionItem.CLIENTREF= Convert.ToInt32(clientRef);
                        else
                            transactionItem.CLIENTREF= 0;
                    }

                    logoMRP.TRANSACTIONS.items.Add(transactionItem);
                }

            }

            logoMRP.LINE_CNT = lineCount;
            if(logoMRP.TRANSACTIONS.items.Count > 0)
            {
                if (await PostMRPPlan(logoMRP))
                {
                    if (splashScreenManager1.IsSplashFormVisible)
                        splashScreenManager1.CloseWaitForm();
                    MessageBox.Show("Üretim Planlama Önerisi başarıyla oluşturuldu!", "Bilgi", MessageBoxButtons.OK);
                }
                else
                {
                    if (splashScreenManager1.IsSplashFormVisible)
                        splashScreenManager1.CloseWaitForm();
                    MessageBox.Show("Üretim Planlama Önerisi oluşturulamadı!", "Hata", MessageBoxButtons.OK);
                }
            }
            else
            {
                if (splashScreenManager1.IsSplashFormVisible)
                    splashScreenManager1.CloseWaitForm();
                MessageBox.Show("Üretim Planlama Önerisi oluşturulamadı! Aktarılacak kayıt bulunamadı.", "Hata", MessageBoxButtons.OK);
            }
           


        }

        private async Task<bool> PostMRPPlan(LogoMRPModels logoMRP)
        {
            try
            {
                string token = await  GetToken();

                var client = new HttpClient();
                client.BaseAddress = new Uri(Program._apiBaseUrl);
                client.DefaultRequestHeaders.Add("Authorization", "Bearer " + token);
                //var response = await client.PostAsync("", logoMRP);

                var json = JsonSerializer.Serialize(logoMRP);
                var httpContent = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await client.PostAsync("demandSlips", httpContent);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new LogoPostException($"MRP planı gönderilirken hata oluştu. Hata: {errorContent}");
                }
                return true;
            }
            catch (LogoAuthException authEx)
            {
                if (splashScreenManager1.IsSplashFormVisible)
                    splashScreenManager1.CloseWaitForm();
                MessageBox.Show(authEx.Message);
                return false;
            }
            catch (LogoPostException postEx)
            {
                if (splashScreenManager1.IsSplashFormVisible)
                    splashScreenManager1.CloseWaitForm();
                MessageBox.Show(postEx.Message);
                return false;
            }
            catch (Exception ex)
            {
                if (splashScreenManager1.IsSplashFormVisible)
                    splashScreenManager1.CloseWaitForm();
                //sonradan loglama eklenecek
                return false;
            }

        }

        private async Task<string> GetToken()
        {
            LogoToken token = null;
            // Use HttpClient and form-url-encoded content which is the typical expected format for OAuth token endpoints.
            // This avoids RestSharp configuration/timeouts issues and matches what Postman usually does.
            // Ensure the base URL ends with a slash
            string baseUrl = Program._apiBaseUrl;
            if (!baseUrl.EndsWith("/"))
                baseUrl += "/";

            using (var http = new HttpClient())
            {
                http.BaseAddress = new Uri(baseUrl);
                // If API expects an API key header, keep it; otherwise remove
                if (!string.IsNullOrEmpty(Program._apiKey))
                {
                    // some APIs expect ApiKey in Authorization header, others a custom header; adjust as needed
                    http.DefaultRequestHeaders.Remove("Authorization");
                    http.DefaultRequestHeaders.Add("Authorization", Program._apiKey);
                }

                var body = $"grant_type=password&username={Uri.EscapeDataString(Program.LogoUser)}&firmno={Uri.EscapeDataString(Program.companyNumber)}&password={Uri.EscapeDataString(Program.LogoPassword)}";
                using (var content = new StringContent(body, Encoding.UTF8, "application/x-www-form-urlencoded"))
                {
                    http.Timeout = TimeSpan.FromSeconds(30);

                    // Post to the absolute path used in Postman
                    var response = await http.PostAsync("token", content).ConfigureAwait(false);

                    var responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    if (!response.IsSuccessStatusCode)
                    {
                        throw new LogoAuthException("Logo API'den token alınırken hata oluştu. Hata:" + responseContent);
                    }

                    token = JsonSerializer.Deserialize<LogoToken>(responseContent);
                    return token?.AccessToken;
                }
            }
        }

        private string GetLastMRPNumber()
        {
            string MRPHeader="MRP";
            string year = DateTime.Now.Year.ToString("0000");
            string month = DateTime.Now.Month.ToString("00");

            string searchPattern = MRPHeader + year + month + "-";

            string lastMRPNumber = "";
            try
            {
                using (SqlConnection connection = new SqlConnection(Program.connectionString))
                {
                    connection.Open();
                    string query = $@"SELECT TOP 1 
                                        CONCAT(
                                            LEFT(FICHENO, 11),  -- 'MRP202511-' kısmını al
                                            RIGHT('00000' + CAST(CAST(RIGHT(FICHENO, 5) AS INT) + 1 AS VARCHAR), 5)  -- Son 5 haneyi 1 arttır ve pad'le
                                        ) AS YENI_FICHENO
                                    FROM LG_{Program.companyNumber}_{Program.periodNumber}_DEMANDFICHE
                                    WHERE FICHENO LIKE '"+ searchPattern + "%' ORDER BY FICHENO DESC";
                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                lastMRPNumber = reader["YENI_FICHENO"].ToString();
                            }
                            else
                            {
                                lastMRPNumber = MRPHeader + year + month + "-000001";
                            }
                            reader.Close();
                        }
                    }
                    connection.Close();
                }
            }
            catch (Exception ex)
            {
                //sonradan loglama eklenecek
            }
            return lastMRPNumber;
        }

        private bool VerifyLogoItem(string itemCode)
        {
            bool status = false;

            try
            {
                using (SqlConnection connection = new SqlConnection(Program.connectionString))
                {
                    connection.Open();
                    string query = $@"SELECT COUNT(*) FROM LG_{Program.companyNumber}_ITEMS WHERE CODE = @ItemCode";
                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@ItemCode", itemCode);
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                int count = reader.GetInt32(0);
                                if (count > 0)
                                    status = true;
                            }
                        }
                    }
                    connection.Close();
                }

            }
            catch (Exception ex)
            {
                //sonradan loglama eklenecek
            }

            return status;

        }

        private void TransferLogoItemSafetyStock(string itemCode, decimal safetyStock, decimal minLevel, decimal maxLevel, string abcCode,string ambar)
        {
            using (SqlConnection connection = new SqlConnection(Program.connectionString))
            {
                connection.Open();
                string query = $@"SELECT ITM.LOGICALREF FROM LG_{Program.companyNumber}_INVDEF INVDEF
                                    LEFT JOIN LG_{Program.companyNumber}_ITEMS ITM ON ITM.LOGICALREF=INVDEF.ITEMREF
                                    WHERE INVDEF.INVENNO=@WareHouse AND ITM.CODE=@ItemCode";
                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@ItemCode", itemCode);
                    command.Parameters.AddWithValue("@WareHouse", 0); //0 varsayılan ambar

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            var itemRef = reader["LOGICALREF"].ToString();
                            UpdateINVDef(itemRef, safetyStock, minLevel, maxLevel, abcCode);
                        }
                        else
                        {
                            InsertINVDef(itemCode, safetyStock, minLevel, maxLevel, abcCode);
                        }
                        
                        reader.Close();
                    }
                }
                connection.Close();
            }
        }

        private void InsertINVDef(string itemCode, decimal safetyStock, decimal minLevel, decimal maxLevel, string abcCode)
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(Program.connectionString))
                {
                    connection.Open();
                    string query = $@"INSERT INTO LG_{Program.companyNumber}_INVDEF 
                            (INVENNO,ITEMREF,MINLEVEL,MAXLEVEL,SAFELEVEL,LOCATIONREF
                            ,PERCLOSEDATE,ABCCODE,MINLEVELCTRL,MAXLEVELCTRL,
                            SAFELEVELCTRL,NEGLEVELCTRL,IOCTRL,VARIANTREF,OUTCTRL)
                            VALUES (0,(SELECT LOGICALREF FROM LG_{Program.companyNumber}_ITEMS WHERE CODE =@ItemCode  AND ACTIVE=0),@MinLevel,@MaxLevel,@SafetyStock,0,NULL,@ABCCODE,0,0,0,0,0,0,0)";
                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        var _abcCode = 0;
                        command.Parameters.AddWithValue("@ItemCode", itemCode);
                        command.Parameters.AddWithValue("@SafetyStock", safetyStock);
                        command.Parameters.AddWithValue("@MinLevel", minLevel);
                        command.Parameters.AddWithValue("@MaxLevel", maxLevel);
                        if (abcCode == "A")
                            _abcCode = 1;
                        else if (abcCode == "B")
                            _abcCode = 2;
                        else if (abcCode == "C")
                            _abcCode = 3;
                        else if (abcCode == "D")
                            _abcCode = 0;
             
                        command.Parameters.AddWithValue("@ABCCODE", _abcCode);
                        command.ExecuteNonQuery();
                        transferredRecordCount++;
                    }
                    connection.Close();
                }
            }
            catch (Exception ex)
            {
                //sonradan loglama eklenecek
            }
        }

        private void UpdateINVDef(string itemRef, decimal safetyStock, decimal minLevel, decimal maxLevel, string abcCode)
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(Program.connectionString))
                {
                    connection.Open();
                    string query = $@"UPDATE LG_{Program.companyNumber}_INVDEF set SAFELEVEL=@SafetyStock,ABCCODE=@ABCCODE,MINLEVEL=@MinLevel,MAXLEVEL=@MaxLevel WHERE INVENNO=0 AND ITEMREF=@itemRef";
                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        var _abcCode = 0;
                        command.Parameters.AddWithValue("@SafetyStock", safetyStock);
                        command.Parameters.AddWithValue("@itemRef", itemRef);
                        command.Parameters.AddWithValue("@MinLevel", minLevel);
                        command.Parameters.AddWithValue("@MaxLevel", maxLevel);

                        if (abcCode == "A")
                            _abcCode = 1;
                        else if (abcCode == "B")
                            _abcCode = 2;
                        else if (abcCode == "C")
                            _abcCode = 3;
                        else if (abcCode == "-")   
                            _abcCode = 0;

                        command.Parameters.AddWithValue("@ABCCODE", _abcCode);

                        command.ExecuteNonQuery();
                        transferredRecordCount++;
                    }
                    connection.Close();
                }
            }
            catch (Exception ex)
            {
               //sonradan loglama eklenecek
            }

        }

        private void excelContentGridView_RowStyle(object sender, DevExpress.XtraGrid.Views.Grid.RowStyleEventArgs e)
        {
            // satırdaki verilerin tipine göre renklendirme işlemi
            if (e.RowHandle >= 0)
            {
                var item = excelContentGridView.GetRowCellValue(e.RowHandle, "Hata").ToString();
                if (item != "")
                {
                    e.Appearance.BackColor = Color.LightCoral;
                }
            }
        }
    }
}
