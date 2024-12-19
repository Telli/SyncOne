using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SyncOne.Models
{
    public class KeywordFilter
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string Keyword { get; set; }
        public bool IsEnabled { get; set; }
        public string Action { get; set; } // "Forward", "Reply", "Block", etc.
        public string ForwardTo { get; set; }
        public string AutoReplyMessage { get; set; }
    }
}
