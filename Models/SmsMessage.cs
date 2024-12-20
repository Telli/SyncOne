using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SyncOne.Models
{
    public class SmsMessage
    {
        public int Id { get; set; }
        public string From { get; set; }
        public string Body { get; set; }
        public DateTime ReceivedAt { get; set; }
        public bool IsProcessed { get; set; }
        public string Response { get; set; }
        public DateTime? ProcessedAt { get; set; }
        public bool IsProcessing { get; internal set; }
        public string SendStatus { get; set; }
    }
}
