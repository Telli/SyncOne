using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.Telephony;
using Microsoft.Extensions.Logging;
using SyncOne.Models;
using SyncOne.Services;
using SmsMessage = SyncOne.Models.SmsMessage;


namespace SyncOne.Platforms.Android.Services
{
    public class AndroidSmsService : ISmsService
    {
        private readonly Context _context;
        private readonly ILogger<AndroidSmsService> _logger;
        private readonly SmsManager _smsManager;

        public event EventHandler<SmsMessage> OnSmsReceived;

        public AndroidSmsService(Context context, ILogger<AndroidSmsService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _smsManager = SmsManager.Default;

            // Register SMS receiver
            SmsReceiver.SetMessageHandler((sender, message) => OnSmsReceived?.Invoke(sender, message));
        }

        public async Task<bool> SendSmsAsync(string phoneNumber, string message)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(phoneNumber))
                    throw new ArgumentException("Phone number cannot be null or empty.", nameof(phoneNumber));

                if (string.IsNullOrWhiteSpace(message))
                    throw new ArgumentException("Message cannot be null or empty.", nameof(message));

                // Check and request SMS permission on the main thread
                bool hasPermission = await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    return await CheckAndRequestSmsPermissionAsync();
                });

                if (!hasPermission)
                {
                    _logger.LogError("SMS permission not granted.");
                    return false;
                }

                // Split message if it's too long
                if (message.Length > 160)
                {
                    var parts = _smsManager.DivideMessage(message);
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
                    _smsManager.SendMultipartTextMessage(
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

                    _smsManager.SendTextMessage(
                        phoneNumber,
                        null,
                        message,
                        sentIntent,
                        deliveredIntent);
                }

                _logger.LogInformation($"SMS sent to {phoneNumber}.");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to send SMS to {phoneNumber}.");
                return false;
            }
        }

        private async Task<bool> CheckAndRequestSmsPermissionAsync()
        {
            try
            {
                var status = await Permissions.CheckStatusAsync<Permissions.Sms>();
                if (status != PermissionStatus.Granted)
                {
                    status = await Permissions.RequestAsync<Permissions.Sms>();
                }
                return status == PermissionStatus.Granted;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check or request SMS permission.");
                return false;
            }
        }

        //public async Task<bool> SendSmsAsync(string phoneNumber, string message)
        //{
        //    try
        //    {
        //        if (string.IsNullOrWhiteSpace(phoneNumber))
        //            throw new ArgumentException("Phone number cannot be null or empty.", nameof(phoneNumber));

        //        if (string.IsNullOrWhiteSpace(message))
        //            throw new ArgumentException("Message cannot be null or empty.", nameof(message));

        //        // Check and request SMS permission
        //        if (!await CheckAndRequestSmsPermissionAsync())
        //        {
        //            _logger.LogError("SMS permission not granted.");
        //            return false;
        //        }

        //        // Split message if it's too long
        //        if (message.Length > 160)
        //        {
        //            var parts = _smsManager.DivideMessage(message);
        //            var sentIntents = new List<PendingIntent>();
        //            var deliveredIntents = new List<PendingIntent>();

        //            // Create pending intents for each part
        //            for (int i = 0; i < parts.Count; i++)
        //            {
        //                var sentIntent = CreateSentPendingIntent($"SMS_SENT_{i}");
        //                var deliveredIntent = CreateDeliveredPendingIntent($"SMS_DELIVERED_{i}");
        //                sentIntents.Add(sentIntent);
        //                deliveredIntents.Add(deliveredIntent);
        //            }

        //            // Send multipart message
        //            _smsManager.SendMultipartTextMessage(
        //                phoneNumber,
        //                null,
        //                parts,
        //                sentIntents.ToArray(),
        //                deliveredIntents.ToArray());
        //        }
        //        else
        //        {
        //            // Send single message with delivery tracking
        //            var sentIntent = CreateSentPendingIntent("SMS_SENT");
        //            var deliveredIntent = CreateDeliveredPendingIntent("SMS_DELIVERED");

        //            _smsManager.SendTextMessage(
        //                phoneNumber,
        //                null,
        //                message,
        //                sentIntent,
        //                deliveredIntent);
        //        }

        //        _logger.LogInformation($"SMS sent to {phoneNumber}.");
        //        return true;
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, $"Failed to send SMS to {phoneNumber}.");
        //        return false;
        //    }
        //}

        //private async Task<bool> CheckAndRequestSmsPermissionAsync()
        //{
        //    try
        //    {
        //        var status = await Permissions.CheckStatusAsync<Permissions.Sms>();
        //        if (status != PermissionStatus.Granted)
        //        {
        //            status = await Permissions.RequestAsync<Permissions.Sms>();
        //        }
        //        return status == PermissionStatus.Granted;
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Failed to check or request SMS permission.");
        //        return false;
        //    }
        //}

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

        // Register status receivers
        private void RegisterStatusReceivers()
        {
            var sentFilter = new IntentFilter("SMS_SENT");
            var deliveredFilter = new IntentFilter("SMS_DELIVERED");

            _context.RegisterReceiver(
                new SmsBroadcastReceiver((success, message) =>
                    _logger.LogInformation($"SMS sent status: {success}, {message}")),
                sentFilter);

            _context.RegisterReceiver(
                new SmsBroadcastReceiver((success, message) =>
                    _logger.LogInformation($"SMS delivery status: {success}, {message}")),
                deliveredFilter);
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
    }
}