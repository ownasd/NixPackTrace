using System;

namespace NixPackTrace.Models
{
    public class DispatchRecord
    {
        public long ID { get; set; } // Auto-increment for local DB
        public string DispatchId { get; set; } = Guid.NewGuid().ToString("N"); // Unique ID for Firebase
        public string FromBoxNo { get; set; } = "";
        public string ToBoxNo { get; set; } = "";
        public DateTime DispatchDate { get; set; } = DateTime.Now;
        public string DispatchedBy { get; set; } = "";
        public string Remarks { get; set; } = "";
        public string SYNC_STATUS { get; set; } = "Pending";
    }
}
