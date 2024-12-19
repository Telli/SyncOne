using SyncOne.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SmsMessage = SyncOne.Models.SmsMessage;

namespace SyncOne.Services
{
    public interface ISmsService
    {
        Task<bool> RequestPermissionsAsync();
        Task<bool> SendSmsAsync(string phoneNumber, string message);
        event EventHandler<SmsMessage> OnSmsReceived;
    }
}
