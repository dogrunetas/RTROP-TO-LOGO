using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TigerStockLevelManager.Models
{
    // Root model that represents the provided JSON structure
    public class LogoMRPModels
    {
        public string NUMBER { get; set; }
        public DateTime DATE { get; set; }
        public long TIME { get; set; }
        public int STATUS { get; set; }
        public int XML_ATTRIBUTE { get; set; }
        public int DEMAND_TYPE { get; set; }
        public int USER_NO { get; set; }
        public string FICHENO { get; set; }
        public int DEMANDTYPE { get; set; }
        public int USERNO { get; set; }
        public Transactions TRANSACTIONS { get; set; }
        public string MPS_CODE { get; set; }
        public int LINE_CNT { get; set; }
    }

    public class Transactions
    {
        // JSON uses lowercase "items" so keep the same name to match directly
        public List<TransactionItem> items { get; set; }
    }

    public class TransactionItem
    {
        public int ITEMREF { get; set; }
        public int CLIENTREF { get; set; }
        public double AMOUNT { get; set; }
        public int MEET_TYPE { get; set; }
        public int STATUS { get; set; }
        public int XML_ATTRIBUTE { get; set; }
        public int SOURCE_INDEX { get; set; }
        public int LINE_NO { get; set; }
        public int MRP_HEAD_TYPE { get; set; }
        public string UNIT_CODE { get; set; }
        public int PORDER_TYPE { get; set; }
        public int BOM_TYPE { get; set; }
        public int BOMMASTERREF { get; set; }
        public int BOMREVREF { get; set; }
    }

}
