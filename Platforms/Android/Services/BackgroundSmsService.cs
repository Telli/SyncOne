using Android.App;
using Android.Content;
using Android.OS;
using System;
using System.Threading.Tasks;
using SyncOne.Models;
using SyncOne.Services;
using Microsoft.Extensions.Logging;
using SmsMessage = SyncOne.Models.SmsMessage;


namespace SyncOne.Platforms.Android.Services
{
    [Service(Name = "com.syncone.SmsService", Exported = true)]
    public class BackgroudSmsService : Service
    {
        private bool _isRunning;
        private readonly ISmsService _smsService;
        private readonly DatabaseService _databaseService;
        private readonly ApiService _apiService;
        private readonly ConfigurationService _configService;
        private readonly ILogger _logger;

        public BackgroudSmsService(ISmsService smsService, DatabaseService databaseService, ApiService apiService, ConfigurationService configService, ILogger<BackgroudSmsService> logger)
        {
            _smsService = smsService;
            _databaseService = databaseService;
            _apiService = apiService;
            _configService = configService;
            _logger = logger;
        }

        public override IBinder OnBind(Intent intent) => null;

        public override void OnCreate()
        {
            base.OnCreate();
            _isRunning = true;
            StartForegroundService();
            Task.Run(() => BackgroundTask());
        }

        public override StartCommandResult OnStartCommand(Intent intent, StartCommandFlags flags, int startId)
        {
            return StartCommandResult.Sticky;
        }

        private void StartForegroundService()
        {
            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            {
                var channel = new NotificationChannel("SyncOneChannel", "SyncOne Service", NotificationImportance.Default)
                {
                    Description = "Service for processing SMS messages in the background"
                };

                var notificationManager = (NotificationManager)GetSystemService(NotificationService);
                notificationManager.CreateNotificationChannel(channel);
            }

            var notification = new Notification.Builder(this, "SyncOneChannel")
                .SetContentTitle("SyncOne Service Running")
                .SetContentText("Processing SMS messages in the background")
                .SetSmallIcon(Resource.Drawable.notification_icon_background)
                .SetOngoing(true)
                .Build();

            StartForeground(1, notification);
        }

        private async Task BackgroundTask()
        {
            while (_isRunning)
            {
                try
                {
                    var messages = await _databaseService.GetUnprocessedMessagesAsync();

                    foreach (var message in messages)
                    {
                        await ProcessMessageAsync(message);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in background task");
                }

                await Task.Delay(TimeSpan.FromSeconds(30));
            }
        }

        private async Task ProcessMessageAsync(SmsMessage message)
        {
            try
            {
                if (!await _configService.IsPhoneNumberAllowedAsync(message.From))
                {
                    await _configService.AddLogEntryAsync("SMS_FILTERED", $"Message from {message.From} was filtered out");
                    return;
                }

                var response = await _apiService.ProcessMessageAsync(message.From, message.Body);

                const int maxRetries = 3;
                int retryCount = 0;
                bool sendSuccess = false;

                while (!sendSuccess && retryCount < maxRetries)
                {
                    sendSuccess = await _smsService.SendSmsAsync(message.From, response);

                    if (!sendSuccess)
                    {
                        retryCount++;
                        if (retryCount < maxRetries)
                        {
                            await Task.Delay(TimeSpan.FromSeconds(2 * retryCount));
                            await _configService.AddLogEntryAsync("SMS_RETRY", $"Retrying SMS send to {message.From}, attempt {retryCount + 1}");
                        }
                    }
                }

                message.Response = response;
                message.IsProcessed = true;
                message.ProcessedAt = DateTime.UtcNow;
                message.SendStatus = sendSuccess ? "Sent" : "Failed";

                await _databaseService.SaveMessageAsync(message);

                if (sendSuccess)
                {
                    await _configService.AddLogEntryAsync("SMS_PROCESSED", $"Successfully sent SMS to {message.From}");
                }
                else
                {
                    await _configService.AddLogEntryAsync("SMS_SEND_FAILED", $"Failed to send SMS to {message.From} after {maxRetries} attempts");
                }
            }
            catch (Exception ex)
            {
                message.SendStatus = "Error";
                await _configService.AddLogEntryAsync("SMS_ERROR", $"Error processing message from {message.From}: {ex.Message}");
                _logger.LogError(ex, "Error processing message");
            }
        }

        public override void OnDestroy()
        {
            _isRunning = false;
            base.OnDestroy();
        }
    }
}
