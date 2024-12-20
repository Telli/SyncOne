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
using Microsoft.Extensions.Logging;

namespace SyncOne.Platforms.Android.Services
{


    [Activity(MainLauncher = true)]
    public class AndroidSmsService : ISmsService
    {
        private readonly Context _context;
        private readonly ILogger _logger;  // Add logging
        public event EventHandler<SmsMessage> OnSmsReceived;

        public AndroidSmsService()
        {
            _context = Platform.CurrentActivity;
            SmsReceiver.SetMessageHandler((sender, message) => OnSmsReceived?.Invoke(sender, message));
        }

        public async Task<bool> SendSmsAsync(string phoneNumber, string message)
        {
            try
            {
                if (!await CheckSmsPermissionAsync())
                {
                    _logger?.LogError("SMS permission not granted");
                    return false;
                }

                // Split message if it's too long
                var smsManager = SmsManager.Default;
                if (message.Length > 160)
                {
                    var parts = smsManager.DivideMessage(message);
                    var sentIntents = new List<PendingIntent>();
                    var deliveredIntents = new List<PendingIntent>();

                    // Create pending intents for each part
                    for (int i = 0; i < parts.Count; i++)
                    {
                        var sentIntent = CreateSentPendingIntent($"SMS_SENT_{i}");
                        var deliveredIntent = CreateDeliveredPendingIntent($"SMS_DELIVERED_{i}");
                        sentIntents.Add(sentIntent);
                        deliveredIntents.Add(deliveredIntent);
                    }

                    // Send multipart message
                    smsManager.SendMultipartTextMessage(
                        phoneNumber,
                        null,
                        parts,
                        sentIntents.ToArray(),
                        deliveredIntents.ToArray());
                }
                else
                {
                    // Send single message with delivery tracking
                    var sentIntent = CreateSentPendingIntent("SMS_SENT");
                    var deliveredIntent = CreateDeliveredPendingIntent("SMS_DELIVERED");

                    smsManager.SendTextMessage(
                        phoneNumber,
                        null,
                        message,
                        sentIntent,
                        deliveredIntent);
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Failed to send SMS to {phoneNumber}");
                return false;
            }
        }
        //public async Task<bool> RequestPermissionsAsync()
        //{
        //    var status = await Permissions.RequestAsync<Permissions.Sms>();
        //    return status == PermissionStatus.Granted;
        //}
        private async Task<bool> CheckSmsPermissionAsync()
        {
            var status = await Permissions.CheckStatusAsync<Permissions.Sms>();
            if (status != PermissionStatus.Granted)
            {
                status = await Permissions.RequestAsync<Permissions.Sms>();
            }
            return status == PermissionStatus.Granted;
        }

        private PendingIntent CreateSentPendingIntent(string action)
        {
            var intent = new Intent(action);
            return PendingIntent.GetBroadcast(
                _context,
                0,
                intent,
                PendingIntentFlags.OneShot | PendingIntentFlags.Immutable);
        }

        private PendingIntent CreateDeliveredPendingIntent(string action)
        {
            var intent = new Intent(action);
            return PendingIntent.GetBroadcast(
                _context,
                0,
                intent,
                PendingIntentFlags.OneShot | PendingIntentFlags.Immutable);
        }

        // SMS broadcast receiver for delivery status
        private class SmsBroadcastReceiver : BroadcastReceiver
        {
            private readonly Action<bool, string> _callback;

            public SmsBroadcastReceiver(Action<bool, string> callback)
            {
                _callback = callback;
            }

            public override void OnReceive(Context context, Intent intent)
            {
                var result = ResultCode;
                var action = intent.Action;

                if (action.StartsWith("SMS_SENT"))
                {
                    switch ((int)result)
                    {
                        case (int)Result.Ok:
                            _callback(true, "SMS sent successfully");
                            break;
                        //case (int)SmsManager.sms:
                        //    _callback(false, "Generic failure");
                        //    break;
                        //case (int)SmsManager.ResultErrorNoService:
                        //    _callback(false, "No service");
                        //    break;
                        //case (int)SmsManager.ResultErrorNullPdu:
                        //    _callback(false, "Null PDU");
                        //    break;
                        //case (int)SmsManager.ResultErrorRadioOff:
                        //    _callback(false, "Radio off");
                        //    break;
                        default:
                            _callback(false, $"Unknown error: {result}");
                            break;
                    }
                }
                else if (action.StartsWith("SMS_DELIVERED"))
                {
                    switch ((int)result)
                    {
                        case (int)Result.Ok:
                            _callback(true, "SMS delivered");
                            break;
                        case (int)Result.Canceled:
                            _callback(false, "SMS not delivered");
                            break;
                    }
                }
            }
        }

        // Register status receivers
        private void RegisterStatusReceivers()
        {
            var sentFilter = new IntentFilter("SMS_SENT");
            var deliveredFilter = new IntentFilter("SMS_DELIVERED");

            _context.RegisterReceiver(
                new SmsBroadcastReceiver((success, message) =>
                    _logger?.LogInformation($"SMS sent status: {success}, {message}")),
                sentFilter);

            _context.RegisterReceiver(
                new SmsBroadcastReceiver((success, message) =>
                    _logger?.LogInformation($"SMS delivery status: {success}, {message}")),
                deliveredFilter);
        }

        public Task<bool> RequestPermissionsAsync()
        {
            throw new NotImplementedException();
        }
    }

}