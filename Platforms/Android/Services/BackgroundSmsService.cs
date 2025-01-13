using Android.App;
using Android.Content;
using Android.OS;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using System;
using System.Threading;
using System.Threading.Tasks;
using SyncOne.Models;
using SyncOne.Services;
using SmsMessage = SyncOne.Models.SmsMessage;
using Resource = Microsoft.Maui.Resource;
using Android.Content.PM;

namespace SyncOne.Platforms.Android.Services
{
    [Service(Name = "SyncOne.Platforms.Android.Services.BackgroundSmsService", Exported = true)]
    public class BackgroundSmsService : Service, IDisposable
    {
        private const int NOTIFICATION_ID = 1;
        private const string CHANNEL_ID = "SyncOneChannel";
        private const int MAX_RETRIES = 3;
        private const int BASE_RETRY_DELAY_SECONDS = 2;
        private const int PROCESSING_INTERVAL_SECONDS = 30;
        private const int ERROR_RETRY_DELAY_SECONDS = 60;

        private readonly SemaphoreSlim _processingSemaphore = new(1, 1);
        private bool _isRunning;
        private bool _disposed;
        private CancellationTokenSource _cancellationTokenSource;
        private readonly ISmsService _smsService;
        private readonly DatabaseService _databaseService;
        private readonly ApiService _apiService;
        private readonly ConfigurationService _configService;
        private readonly ILogger<BackgroundSmsService> _logger;
        private NotificationManager _notificationManager;
        private PowerManager.WakeLock _wakeLock;

        // Constructor Injection
        public BackgroundSmsService()
        {
        }

        public BackgroundSmsService(
            ISmsService smsService,
            DatabaseService databaseService,
            ApiService apiService,
            ConfigurationService configService,
            ILogger<BackgroundSmsService> logger)
        {
            _smsService = smsService ?? throw new ArgumentNullException(nameof(smsService));
            _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
            _apiService = apiService ?? throw new ArgumentNullException(nameof(apiService));
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public override void OnCreate()
        {
            base.OnCreate();

            try
            {
                AcquireWakeLock();
                InitializeNotificationManager();
                StartServiceOperation();
            }
            catch (Exception ex)
            {
                LogCriticalError("Failed to initialize background service", ex);
                StopSelf();
            }
        }

        private void AcquireWakeLock()
        {
            var powerManager = (PowerManager)GetSystemService(PowerService);
            _wakeLock = powerManager?.NewWakeLock(
                WakeLockFlags.Partial,
                "SyncOne::BackgroundServiceLock");
            _wakeLock?.Acquire();
        }

        public override StartCommandResult OnStartCommand(Intent intent, StartCommandFlags flags, int startId)
        {
            try
            {
                _logger?.LogInformation("Background service starting with flags: {Flags}", flags);

                if (!_isRunning)
                {
                    StartServiceOperation();
                }

                return StartCommandResult.Sticky;
            }
            catch (Exception ex)
            {
                LogCriticalError("Failed to start service", ex);
                StopSelf();
                return StartCommandResult.NotSticky;
            }
        }

        public override IBinder OnBind(Intent intent)
        {
            return null;
        }

        private void InitializeNotificationManager()
        {
            try
            {
                _notificationManager = (NotificationManager)GetSystemService(NotificationService)
                    ?? throw new InvalidOperationException("Failed to get NotificationManager");

                if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
                {
                    var channel = new NotificationChannel(
                        CHANNEL_ID,
                        "SyncOne Service",
                        NotificationImportance.Low)
                    {
                        Description = "Background SMS processing service",
                        LockscreenVisibility = NotificationVisibility.Private
                    };

                    _notificationManager.CreateNotificationChannel(channel);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to initialize notification manager", ex);
            }
        }

        private void StartServiceOperation()
        {
            if (_disposed) return;

            _isRunning = true;
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource = new CancellationTokenSource();

            var notification = CreateServiceNotification(
                "SyncOne is running",
                "Processing SMS messages in background");

            // Start the service in the foreground with a valid service type
            StartForeground(NOTIFICATION_ID, notification, ForegroundService.TypeDataSync);

            Task.Run(
                () => BackgroundProcessingLoop(_cancellationTokenSource.Token),
                _cancellationTokenSource.Token);
        }

        private Notification CreateServiceNotification(string title, string text)
        {
            var notificationBuilder = new Notification.Builder(this, CHANNEL_ID)
                .SetContentTitle(title)
                .SetContentText(text)
                .SetOngoing(true)
                .SetCategory(Notification.CategoryService)
                .SetPriority((int)NotificationPriority.Low)
                .SetSmallIcon(Resource.Drawable.ic_stat_notifications_active);

            if (Build.VERSION.SdkInt >= BuildVersionCodes.S)
            {
                notificationBuilder.SetForegroundServiceBehavior(
                    (int)NotificationForegroundService.Immediate);
            }

            return notificationBuilder.Build();
        }

        private async Task BackgroundProcessingLoop(CancellationToken cancellationToken)
        {
            while (_isRunning && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessPendingMessages(cancellationToken);
                    await Task.Delay(
                        TimeSpan.FromSeconds(PROCESSING_INTERVAL_SECONDS),
                        cancellationToken);
                }
                catch (System.OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    _logger?.LogInformation("Background processing loop cancelled");
                    break;
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error in background processing loop");
                    await Task.Delay(
                        TimeSpan.FromSeconds(ERROR_RETRY_DELAY_SECONDS),
                        CancellationToken.None);
                }
            }
        }

        private async Task ProcessPendingMessages(CancellationToken cancellationToken)
        {
            if (_disposed) return;

            await _processingSemaphore.WaitAsync(cancellationToken);
            try
            {
                var messages = await _databaseService.GetUnprocessedMessagesAsync();
                foreach (var message in messages)
                {
                    if (cancellationToken.IsCancellationRequested) break;

                    try
                    {
                        await ProcessSingleMessage(message);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Failed to process message {MessageId}", message.Id);
                    }
                }
            }
            finally
            {
                _processingSemaphore.Release();
            }
        }

        private async Task ProcessSingleMessage(SmsMessage message)
        {
            try
            {
                if (!await _configService.IsPhoneNumberAllowedAsync(message.From))
                {
                    await _configService.AddLogEntryAsync("SMS_FILTERED",
                        $"Message from {message.From} was filtered out");
                    return;
                }

                await MarkMessageAsProcessing(message);

                var apiResponse = await _apiService.ProcessMessageAsync(message.From, message.Body);

                var sendSuccess = await SendSmsWithRetry(message.From, apiResponse.Response);

                await UpdateMessageStatus(message, apiResponse, sendSuccess);
                await LogProcessingResult(message, sendSuccess);
            }
            catch (ApiException ex)
            {
                _logger?.LogError(ex, "API processing failed for message {MessageId}", message.Id);
                await HandleProcessingError(message, ex);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Unexpected error processing message {MessageId}", message.Id);
                await HandleProcessingError(message, ex);
            }
        }

        private async Task MarkMessageAsProcessing(SmsMessage message)
        {
            message.IsProcessing = true;
            await _databaseService.SaveMessageAsync(message);
        }

        private async Task<bool> SendSmsWithRetry(string phoneNumber, string messageText)
        {
            // Exponential Backoff with Jitter
            var retryPolicy = Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(MAX_RETRIES,
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(BASE_RETRY_DELAY_SECONDS, retryAttempt - 1)
                                                       + new Random().Next(0, BASE_RETRY_DELAY_SECONDS)),
                    (exception, timeSpan, retryCount, context) =>
                    {
                        _logger?.LogError(exception,
                            "Error sending SMS attempt {RetryCount}/{MaxRetries}. Retrying in {TimeSpan} seconds.",
                            retryCount, MAX_RETRIES, timeSpan);
                    });

            // Execute the SMS sending operation with the retry policy
            return await retryPolicy.ExecuteAsync(async () =>
                await _smsService.SendSmsAsync(phoneNumber, messageText));
        }

        private async Task UpdateMessageStatus(
            SmsMessage message,
            ApiResponse apiResponse,
            bool sendSuccess)
        {
            message.Response = apiResponse.Response;
            message.IsProcessed = true;
            message.IsProcessing = false;
            message.ProcessedAt = DateTime.UtcNow;
            message.SendStatus = sendSuccess ? "Sent" : "Failed";
            message.ApiStatus = apiResponse.Status;
            message.ExternalId = apiResponse.MessageId;

            await _databaseService.SaveMessageAsync(message);
        }

        private async Task LogProcessingResult(SmsMessage message, bool sendSuccess)
        {
            if (sendSuccess)
            {
                await _configService.AddLogEntryAsync("SMS_PROCESSED",
                    $"Successfully processed and sent message to {message.From}");
            }
            else
            {
                await _configService.AddLogEntryAsync("SMS_SEND_FAILED",
                    $"Failed to send SMS to {message.From} after {MAX_RETRIES} attempts");
            }
        }

        private async Task HandleProcessingError(SmsMessage message, Exception ex)
        {
            message.IsProcessing = false;
            message.SendStatus = "Error";

            await _configService.AddLogEntryAsync("SMS_ERROR",
                $"Error processing message from {message.From}: {ex.Message}");

            _logger?.LogError(ex, "Failed to process message from {From}", message.From);
        }

        private void LogCriticalError(string message, Exception ex)
        {
            _logger?.LogCritical(ex, message);
            System.Diagnostics.Debug.WriteLine($"{message}: {ex}");
        }

        public override void OnDestroy()
        {
            try
            {
                _logger?.LogInformation("Background service being destroyed");
                Dispose();
            }
            finally
            {
                base.OnDestroy();
            }
        }

        public override void OnTaskRemoved(Intent rootIntent)
        {
            try
            {
                _logger?.LogInformation("Background service task removed");

                var restartServiceIntent = new Intent(ApplicationContext, typeof(BackgroundSmsService));
                restartServiceIntent.SetPackage(PackageName);

                var pendingIntent = PendingIntent.GetService(
                    ApplicationContext, 1, restartServiceIntent,
                    PendingIntentFlags.OneShot | PendingIntentFlags.Immutable);

                var alarmManager = (AlarmManager)GetSystemService(AlarmService);
                alarmManager?.Set(
                    AlarmType.RtcWakeup,
                    SystemClock.ElapsedRealtime() + 1000,
                    pendingIntent);
            }
            catch (Exception ex)
            {
                LogCriticalError("Failed to handle task removal", ex);
            }
            finally
            {
                base.OnTaskRemoved(rootIntent);
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _isRunning = false;
                    _cancellationTokenSource?.Cancel();
                    _cancellationTokenSource?.Dispose();
                    _processingSemaphore?.Dispose();
                    _wakeLock?.Release();
                    _wakeLock?.Dispose();
                }
                _disposed = true;
            }
        }
    }
}