using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Permissions;
using SyncOne.Models;
using SmsMessage = SyncOne.Models.SmsMessage;
using Android.App;
using Android.Telephony;
using Android.Content;
using SyncOne.Services;

namespace SyncOne.Platforms.Android.Services
{


    [Activity(MainLauncher = true)]
    public class AndroidSmsService : ISmsService
    {
        private readonly Context _context;
        public event EventHandler<SmsMessage> OnSmsReceived;

        public AndroidSmsService()
        {
            _context = Platform.CurrentActivity;
            SmsReceiver.SetMessageHandler((sender, message) => OnSmsReceived?.Invoke(sender, message));

        }

        public async Task<bool> RequestPermissionsAsync()
        {
            var status = await Permissions.RequestAsync<Permissions.Sms>();
            return status == PermissionStatus.Granted;
        }

        public async Task<bool> SendSmsAsync(string phoneNumber, string message)
        {
            try
            {
                SmsManager.Default.SendTextMessage(phoneNumber, null, message, null, null);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        // Register SMS receiver in OnCreate
        private void RegisterSmsReceiver()
        {
            var filter = new IntentFilter();
            filter.AddAction("android.provider.Telephony.SMS_RECEIVED");
            _context.RegisterReceiver(new SmsReceiver(OnSmsReceived), filter);
        }
    }

}