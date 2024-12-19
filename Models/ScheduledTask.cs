using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SyncOne.Models
{
    public class ScheduledTask
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string Type { get; set; } // "Sync", "Message", "Delete", etc.
        public string Parameters { get; set; } // JSON string of parameters
        public DateTime NextRunTime { get; set; }
        public string Frequency { get; set; } // "Once", "Daily", "Weekly", etc.
        public bool IsEnabled { get; set; }
        public DateTime? LastRun { get; set; }
    }
}
