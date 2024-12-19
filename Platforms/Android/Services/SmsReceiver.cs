using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.Provider;
using SyncOne.Models;
using SmsMessage = SyncOne.Models.SmsMessage;

namespace SyncOne.Platforms.Android.Services
{
    [BroadcastReceiver(Enabled = true, Exported = true)]
    [IntentFilter(new[] { "android.provider.Telephony.SMS_RECEIVED" })]
    public class SmsReceiver : BroadcastReceiver
    {
        private static EventHandler<SmsMessage> _onSmsReceived;
        public SmsReceiver()
        {
            
        }

        public SmsReceiver(EventHandler<SmsMessage> onSmsReceived)
        {
            _onSmsReceived = onSmsReceived;
        }
        public static void SetMessageHandler(EventHandler<SmsMessage> handler)
        {
            _onSmsReceived = handler;
        }
        public override void OnReceive(Context context, Intent intent)
        {
            if (intent.Action != "android.provider.Telephony.SMS_RECEIVED") return;

            var messages = Telephony.Sms.Intents.GetMessagesFromIntent(intent);
            foreach (var message in messages)
            {
                var smsMessage = new SmsMessage
                {
                    From = message.OriginatingAddress,
                    Body = message.MessageBody,
                    ReceivedAt = DateTime.UtcNow,
                    IsProcessed = false
                };
                _onSmsReceived?.Invoke(this, smsMessage);
            }
        }
    }
}
