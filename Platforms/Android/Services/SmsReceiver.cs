using System;
using Android.App;
using Android.Content;
using Android.Provider;
using Microsoft.Extensions.Logging;
using SyncOne.Models;
using SmsMessage = SyncOne.Models.SmsMessage;

namespace SyncOne.Platforms.Android.Services
{
    [BroadcastReceiver(Enabled = true, Exported = true)]
    [IntentFilter(new[] { "android.provider.Telephony.SMS_RECEIVED" })]
    public class SmsReceiver : BroadcastReceiver
    {
        private static EventHandler<SmsMessage> _onSmsReceived;
        private readonly ILogger<SmsReceiver> _logger;

        public SmsReceiver()
        {
            // Default constructor required for Android
        }

        public SmsReceiver(EventHandler<SmsMessage> onSmsReceived, ILogger<SmsReceiver> logger)
        {
            _onSmsReceived = onSmsReceived ?? throw new ArgumentNullException(nameof(onSmsReceived));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public static void SetMessageHandler(EventHandler<SmsMessage> handler)
        {
            _onSmsReceived = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        public override void OnReceive(Context context, Intent intent)
        {
            try
            {
                if (intent?.Action != "android.provider.Telephony.SMS_RECEIVED")
                {
                    _logger?.LogWarning("Received intent with unexpected action: {Action}", intent?.Action);
                    return;
                }

                var messages = Telephony.Sms.Intents.GetMessagesFromIntent(intent);
                if (messages == null || !messages.Any())
                {
                    _logger?.LogWarning("No SMS messages found in the intent.");
                    return;
                }

                foreach (var message in messages)
                {
                    try
                    {
                        var smsMessage = new SmsMessage
                        {
                            From = message.OriginatingAddress,
                            Body = message.MessageBody,
                            ReceivedAt = DateTime.UtcNow,
                            IsProcessed = false
                        };

                        _logger?.LogInformation("Received SMS from {From}: {Body}", smsMessage.From, smsMessage.Body);

                        // Trigger the event on a background thread
                        Task.Run(() => _onSmsReceived?.Invoke(this, smsMessage));
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Failed to process SMS from {From}", message.OriginatingAddress);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Unexpected error in SMS receiver.");
            }
        }
    }
}