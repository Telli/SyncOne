using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SyncOne.Models
{
    public class AppSettings
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string DeviceId { get; set; }
        public string DeviceName { get; set; }
        public string AlertPhoneNumber { get; set; }
        public bool EnableDeliveryReport { get; set; }
        public bool EnableTaskChecking { get; set; }
        public bool EnableMessageResults { get; set; }
        public bool EnableAutoSync { get; set; }
        public int AutoSyncFrequency { get; set; } // in minutes
        public bool AutoDeleteMessages { get; set; }
        public bool AutoDeletePendingMessages { get; set; }
        public int PendingMessageRetries { get; set; }
        public string DefaultLanguage { get; set; }
    }

}
