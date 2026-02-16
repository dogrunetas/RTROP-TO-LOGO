using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TigerStockLevelManager
{
    internal static class Program
    {
        public static string server="UTKU",
            database="DEVA",
            uid="sa",
            password="1";
        public static string connectionString;

        public static string companyNumber = "125";
        public static string periodNumber = "01";

        public static string mmWarehouse = "1", ymWarehouse = "2",hmWarehouse="3";

        public static string LogoUser="LOGO", LogoPassword="5346",LogoUserNumber="1";

        public static string _apiBaseUrl = "http://localhost:32001/api/v1/";

        public static string _apiKey = "Basic RE9HUlVORVQ6VGZRY2ErNlRWbzdVY1Y1VXFCd3pkZDllS1JnRXRUQUI3OUZyL2lUWXAvUT0=";

        public static string startupPath = Application.StartupPath;

        [STAThread]
        static void Main()
        {
            SaveINI saveINI = new SaveINI(startupPath + "\\config.ini");

            server = saveINI.Oku("Database", "Server");
            database = saveINI.Oku("Database", "Database");
            uid = saveINI.Oku("Database", "Uid");
            password = saveINI.Oku("Database", "Password");
            
            LogoUser = saveINI.Oku("Logo", "LogoUser");
            LogoPassword = saveINI.Oku("Logo", "LogoPassword");
            LogoUserNumber = saveINI.Oku("Logo", "LogoUserNumber");
            companyNumber = saveINI.Oku("Logo", "CompanyNumber");
            periodNumber = saveINI.Oku("Logo", "PeriodNumber");
            //_apiBaseUrl = saveINI.Oku("Logo", "APIBaseUrl");

            mmWarehouse = saveINI.Oku("Warehouses", "MMAmbar");
            ymWarehouse = saveINI.Oku("Warehouses", "YMAmbar");
            hmWarehouse = saveINI.Oku("Warehouses", "HMAmbar");

            //MessageBox.Show("Server: " + server + "\nDatabase: " + database + "\nUid: " + uid + "\nPassword: " + password +
            //    "\n\nLogoUser: " + LogoUser + "\nLogoPassword: " + LogoPassword + "\nLogoUserNumber: " + LogoUserNumber +
            //    "\n\nCompanyNumber: " + companyNumber + "\nPeriodNumber: " + periodNumber +
            //    "\n\nMM Ambar: " + mmWarehouse + "\nYM Ambar: " + ymWarehouse + "\nHM Ambar: " + hmWarehouse, "Configuration Values");

            connectionString =$"Server={server};Database={database};User Id={uid};Password={password};";
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());
        }
    }
}
