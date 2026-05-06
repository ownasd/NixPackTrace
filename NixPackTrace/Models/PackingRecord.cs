using System;

namespace NixPackTrace.Models
{
    public class PackingRecord
    {
        public long ID { get; set; } // Auto-increment for local
        public string MAC_ID { get; set; } = "";
        public int MAC_LENGTH { get; set; }
        public string LONG_QR { get; set; } = "";
        public int QR_LENGTH { get; set; }
        public string SHORT_QR { get; set; } = "";
        public string TESTING_QR { get; set; } = "";
        public int TESTING_QR_LENGTH { get; set; }
        public string BOX_NO { get; set; } = "";
        public string STATUS { get; set; } = "OK";
        public DateTime TIMESTAMP { get; set; } = DateTime.Now;
        public string PACKED_BY { get; set; } = "";
        public string Remarks { get; set; } = "";
        public string SYNC_STATUS { get; set; } = "Pending";

        // Joined properties for Dispatch tracking
        public DateTime? DispatchDate { get; set; }
        public string? DispatchBy { get; set; }
        public string? DispatchRemarks { get; set; }
    }
}
