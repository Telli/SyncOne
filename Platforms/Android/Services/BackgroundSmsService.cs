using Android.App;
using Android.Content;
using Android.OS;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using SyncOne.Models;
using SyncOne.Services;
using System.Text.Json;
using SmsMessage = SyncOne.Models.SmsMessage;

namespace SyncOne.Platforms.Android.Services
{
    [Service(Name = "syncone.platforms.android.services.BackgroundSmsService", Exported = true)]
    public class BackgroundSmsService : Service, IDisposable
    {
        private const int NOTIFICATION_ID = 1;
        private const string CHANNEL_ID = "SyncOneChannel";
        private const int MAX_RETRIES = 3;
        private const int BASE_RETRY_DELAY_SECONDS = 2;
        private const int PROCESSING_INTERVAL_SECONDS = 30;
        private const int ERROR_RETRY_DELAY_SECONDS = 60;

        private readonly SemaphoreSlim _processingSemaphore = new SemaphoreSlim(1, 1);
        private bool _isRunning;
        private bool _disposed;
        private CancellationTokenSource _cancellationTokenSource;
        private ISmsService _smsService;
        private DatabaseService _databaseService;
        private ApiService _apiService;
        private ConfigurationService _configService;
        private ILogger<BackgroundSmsService> _logger;
        private NotificationManager _notificationManager;
        private PowerManager.WakeLock _wakeLock;

        public override void OnCreate()
        {
            base.OnCreate();

            try
            {
                AcquireWakeLock();
                InitializeServices();
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

        private void InitializeServices()
        {
            try
            {
                var serviceProvider = MauiApplication.Current?.Services
                    ?? throw new InvalidOperationException("Application services not initialized");

                _smsService = serviceProvider.GetRequiredService<ISmsService>();
                _databaseService = serviceProvider.GetRequiredService<DatabaseService>();
                _apiService = serviceProvider.GetRequiredService<ApiService>();
                _configService = serviceProvider.GetRequiredService<ConfigurationService>();
                _logger = serviceProvider.GetRequiredService<ILogger<BackgroundSmsService>>();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to initialize required services", ex);
            }
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

            StartForeground(NOTIFICATION_ID, notification);

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
                .SetPriority((int)NotificationPriority.Low);

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
                        // Continue processing other messages
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

                message.IsProcessing = true;
                await _databaseService.SaveMessageAsync(message);

                var apiResponse = await ProcessApiRequestWithRetry(message);
                if (apiResponse == null) return;

                var sendSuccess = await SendSmsWithRetry(message.From, apiResponse.Response);

                await UpdateMessageStatus(message, apiResponse, sendSuccess);
                await LogProcessingResult(message, sendSuccess);
            }
            catch (Exception ex)
            {
                await HandleProcessingError(message, ex);
                throw;
            }
        }

        private async Task<ApiResponse> ProcessApiRequestWithRetry(SmsMessage message)
        {
            for (int attempt = 1; attempt <= MAX_RETRIES; attempt++)
            {
                try
                {
                    var response = await _apiService.ProcessMessageAsync(message.From, message.Body);
                    var apiResponse = JsonSerializer.Deserialize<ApiResponse>(response);

                    if (apiResponse?.Response == null)
                    {
                        throw new InvalidOperationException("Invalid API response format");
                    }

                    return apiResponse;
                }
                catch (Exception ex) when (attempt < MAX_RETRIES)
                {
                    var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt - 1));
                    _logger?.LogWarning(ex,
                        "API request attempt {Attempt} failed for message {MessageId}",
                        attempt, message.Id);
                    await Task.Delay(delay);
                }
            }

            throw new InvalidOperationException(
                $"Failed to process API request after {MAX_RETRIES} attempts");
        }

        private async Task<bool> SendSmsWithRetry(string phoneNumber, string message)
        {
            for (int attempt = 1; attempt <= MAX_RETRIES; attempt++)
            {
                try
                {
                    var success = await _smsService.SendSmsAsync(phoneNumber, message);
                    if (success) return true;

                    if (attempt < MAX_RETRIES)
                    {
                        var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt - 1));
                        await _configService.AddLogEntryAsync("SMS_RETRY",
                            $"Retrying SMS send to {phoneNumber}, attempt {attempt + 1}");
                        await Task.Delay(delay);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex,
                        "Error sending SMS attempt {Attempt}/{MaxRetries}",
                        attempt, MAX_RETRIES);
                }
            }
            return false;
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

        private class ApiResponse
        {
            public string Response { get; set; }
            public string Status { get; set; }
            public string MessageId { get; set; }
            public DateTime Timestamp { get; set; }
        }
    }
}