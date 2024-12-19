using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SyncOne.Models
{
    public class LogEntry
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public DateTime Timestamp { get; set; }
        public string Type { get; set; } // "Info", "Error", "Warning"
        public string Component { get; set; }
        public string Message { get; set; }
        public string Details { get; set; }
    }
}
