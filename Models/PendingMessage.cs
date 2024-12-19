using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SyncOne.Models
{
    public class PendingMessage
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string To { get; set; }
        public string Body { get; set; }
        public DateTime CreatedAt { get; set; }
        public int RetryCount { get; set; }
        public string Status { get; set; } // "Pending", "Failed", "Sent"
        public string ErrorMessage { get; set; }
        public DateTime? LastRetry { get; set; }
    }
}
