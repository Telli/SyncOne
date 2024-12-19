using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Maui.Controls;
using System.Text.Json;
using System.Text;
using SQLite;

namespace SyncOne.Models
{
    public class AppConfig
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string ApiUrl { get; set; }
        public bool UseAllowlist { get; set; } // If true, only allowed numbers can interact
        public bool UseBlocklist { get; set; } // If true, blocked numbers cannot interact
        public string DeviceId { get;  set; }
        public bool EnableAutoSync { get; set; }
    }
}
