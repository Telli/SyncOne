using Microsoft.Extensions.Hosting;
using System.Collections.Concurrent;
using SyncOne.Models;
using SyncOne.Services;
using SmsMessage = SyncOne.Models.SmsMessage;

namespace SyncOne.Services
{
    public class BackgroundSmsService : BackgroundService
    {
        private readonly ISmsService _smsService;
        private readonly DatabaseService _databaseService;
        private readonly ApiService _apiService;
        private readonly ConfigurationService _configService;
        private readonly ConcurrentDictionary<string, Action<SmsMessage>> _messageUpdateCallbacks;

        public BackgroundSmsService(
            ISmsService smsService,
            DatabaseService databaseService,
            ApiService apiService,
            ConfigurationService configService)
        {
            _smsService = smsService;
            _databaseService = databaseService;
            _apiService = apiService;
            _configService = configService;
            _messageUpdateCallbacks = new ConcurrentDictionary<string, Action<SmsMessage>>();
        }

        public void RegisterForUpdates(string subscriberId, Action<SmsMessage> callback)
        {
            _messageUpdateCallbacks.TryAdd(subscriberId, callback);
        }

        public void UnregisterFromUpdates(string subscriberId)
        {
            _messageUpdateCallbacks.TryRemove(subscriberId, out _);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await _databaseService.InitializeAsync();

            _smsService.OnSmsReceived += async (sender, message) =>
            {
                await ProcessIncomingSmsAsync(message);
            };

            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }

        private async Task ProcessIncomingSmsAsync(SmsMessage message)
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
                NotifySubscribers(message);

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
                            await _configService.AddLogEntryAsync("SMS_RETRY",
                                $"Retrying SMS send to {message.From}, attempt {retryCount + 1}");
                        }
                    }
                }

                message.Response = response;
                message.IsProcessed = true;
                message.IsProcessing = false;
                message.ProcessedAt = DateTime.UtcNow;
                message.SendStatus = sendSuccess ? "Sent" : "Failed";

                await _databaseService.SaveMessageAsync(message);
                NotifySubscribers(message);

                if (sendSuccess)
                {
                    await _configService.AddLogEntryAsync("SMS_PROCESSED",
                        $"Successfully processed and sent message to {message.From}");
                }
                else
                {
                    await _configService.AddLogEntryAsync("SMS_SEND_FAILED",
                        $"Failed to send SMS to {message.From} after {maxRetries} attempts");
                }
            }
            catch (Exception ex)
            {
                message.IsProcessing = false;
                message.SendStatus = "Error";
                await _configService.AddLogEntryAsync("SMS_ERROR",
                    $"Error processing message from {message.From}: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Error processing message: {ex.Message}");
                NotifySubscribers(message);
            }
        }

        private void NotifySubscribers(SmsMessage message)
        {
            foreach (var callback in _messageUpdateCallbacks.Values)
            {
                try
                {
                    callback(message);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error notifying subscriber: {ex.Message}");
                }
            }
        }
    }
}