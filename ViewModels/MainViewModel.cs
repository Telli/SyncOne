using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using SyncOne.Models;
using SyncOne.Services;
using System.Threading;
using SmsMessage = SyncOne.Models.SmsMessage;

namespace SyncOne.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly ISmsService _smsService;
        private readonly DatabaseService _databaseService;
        private readonly ApiService _apiService;
        private readonly ConfigurationService _configService;
        private readonly IServiceProvider _serviceProvider;
        private readonly SemaphoreSlim _processingLock = new SemaphoreSlim(1, 1);
        private bool _disposed;

        private ObservableCollection<SmsMessage> _messages;
        private bool _isRefreshing;
        private bool _hasError;
        private string _errorMessage;
        private CancellationTokenSource _refreshCancellation;

        public Command OpenSettingsCommand { get; }
        public Command RefreshCommand { get; }

        public ObservableCollection<SmsMessage> Messages
        {
            get => _messages;
            private set
            {
                if (_messages != value)
                {
                    _messages = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsRefreshing
        {
            get => _isRefreshing;
            private set
            {
                if (_isRefreshing != value)
                {
                    _isRefreshing = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool HasError
        {
            get => _hasError;
            private set
            {
                if (_hasError != value)
                {
                    _hasError = value;
                    OnPropertyChanged();
                }
            }
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            private set
            {
                if (_errorMessage != value)
                {
                    _errorMessage = value;
                    OnPropertyChanged();
                    HasError = !string.IsNullOrEmpty(value);
                }
            }
        }

        public MainViewModel(
            IServiceProvider serviceProvider,
            ISmsService smsService,
            DatabaseService databaseService,
            ApiService apiService,
            ConfigurationService configurationService)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _smsService = smsService ?? throw new ArgumentNullException(nameof(smsService));
            _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
            _apiService = apiService ?? throw new ArgumentNullException(nameof(apiService));
            _configService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));

            _messages = new ObservableCollection<SmsMessage>();
            _refreshCancellation = new CancellationTokenSource();

            RefreshCommand = new Command(async () => await RefreshMessagesAsync(), () => !IsRefreshing);

            // Initialize asynchronously but don't await
            Initialize();

            // Safe event subscription with weak reference
            _smsService.OnSmsReceived += HandleSmsReceived;
        }

        private async void Initialize()
        {
            try
            {
                await InitializeDatabaseAsync();
                await LoadMessagesAsync();
            }
            catch (Exception ex)
            {
                HandleError("Initialization failed", ex);
            }
        }

        private async Task InitializeDatabaseAsync()
        {
            try
            {
                await _databaseService.InitializeAsync();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to initialize database", ex);
            }
        }

        private void HandleSmsReceived(object sender, SmsMessage message)
        {
            if (_disposed) return;

            Task.Run(async () =>
            {
                try
                {
                    await ProcessIncomingSmsAsync(message);
                }
                catch (Exception ex)
                {
                    await HandleError("Failed to process incoming message", ex);
                }
            });
        }

        private async Task RefreshMessagesAsync()
        {
            if (IsRefreshing) return;

            try
            {
                _refreshCancellation?.Cancel();
                _refreshCancellation = new CancellationTokenSource();
                var token = _refreshCancellation.Token;

                IsRefreshing = true;
                await LoadMessagesAsync();
                ErrorMessage = null;
            }
            catch (OperationCanceledException)
            {
                // Refresh was cancelled, ignore
            }
            catch (Exception ex)
            {
                await HandleError("Failed to refresh messages", ex);
            }
            finally
            {
                IsRefreshing = false;
            }
        }

        private async Task ProcessIncomingSmsAsync(SmsMessage message)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));

            await _processingLock.WaitAsync();
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

                await AddMessageToCollectionAsync(message);

                var response = await ProcessMessageWithRetryAsync(message);
                if (response == null) return; // Processing failed or was cancelled

                var sendSuccess = await SendSmsWithRetryAsync(message.From, response);
                await UpdateMessageStatusAsync(message, response, sendSuccess);
                await LogProcessingResultAsync(message, sendSuccess);
            }
            catch (Exception ex)
            {
                await HandleMessageError(message, ex);
            }
            finally
            {
                _processingLock.Release();
            }
        }

        private async Task<string> ProcessMessageWithRetryAsync(SmsMessage message)
        {
            const int maxRetries = 3;
            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    return await _apiService.ProcessMessageAsync(message.From, message.Body);
                }
                catch (Exception ex) when (i < maxRetries - 1)
                {
                    await _configService.AddLogEntryAsync("API_RETRY",
                        $"Retrying API call for {message.From}, attempt {i + 2}/{maxRetries}");
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, i))); // Exponential backoff
                }
            }
            return null;
        }

        private async Task<bool> SendSmsWithRetryAsync(string phoneNumber, string messageText)
        {
            const int maxRetries = 3;
            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    var success = await _smsService.SendSmsAsync(phoneNumber, messageText);
                    if (success) return true;

                    if (i < maxRetries - 1)
                    {
                        var delay = TimeSpan.FromSeconds(Math.Pow(2, i)); // Exponential backoff
                        await _configService.AddLogEntryAsync("SMS_RETRY",
                            $"Retrying SMS send to {phoneNumber}, attempt {i + 2}/{maxRetries}");
                        await Task.Delay(delay);
                    }
                }
                catch (Exception ex) when (i < maxRetries - 1)
                {
                    await _configService.AddLogEntryAsync("SMS_ERROR",
                        $"Error sending SMS to {phoneNumber}: {ex.Message}");
                }
            }
            return false;
        }

        private async Task AddMessageToCollectionAsync(SmsMessage message)
        {
            if (_disposed) return;

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (Messages != null && !_disposed)
                {
                    Messages.Insert(0, message);
                }
            });
        }

        private async Task UpdateMessageStatusAsync(SmsMessage message, string response, bool sendSuccess)
        {
            message.Response = response;
            message.IsProcessed = true;
            message.IsProcessing = false;
            message.ProcessedAt = DateTime.UtcNow;
            message.SendStatus = sendSuccess ? "Sent" : "Failed";

            await _databaseService.SaveMessageAsync(message);

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (Messages != null && !_disposed)
                {
                    var existingMessage = Messages.FirstOrDefault(m => m.Id == message.Id);
                    if (existingMessage != null)
                    {
                        var index = Messages.IndexOf(existingMessage);
                        Messages[index] = message;
                    }
                }
            });
        }

        private async Task LogProcessingResultAsync(SmsMessage message, bool sendSuccess)
        {
            if (sendSuccess)
            {
                await _configService.AddLogEntryAsync("SMS_PROCESSED",
                    $"Successfully processed and sent message to {message.From}");
            }
            else
            {
                await _configService.AddLogEntryAsync("SMS_SEND_FAILED",
                    $"Failed to send SMS to {message.From}");
            }
        }

        private async Task HandleMessageError(SmsMessage message, Exception ex)
        {
            message.IsProcessing = false;
            message.SendStatus = "Error";

            await _configService.AddLogEntryAsync("SMS_ERROR",
                $"Error processing message from {message.From}: {ex.Message}");

            await HandleError($"Error processing message from {message.From}", ex);
        }

        private async Task LoadMessagesAsync()
        {
            try
            {
                var messages = await _databaseService.GetMessagesAsync();
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    if (!_disposed)
                    {
                        Messages.Clear();
                        foreach (var message in messages.OrderByDescending(m => m.ReceivedAt))
                        {
                            Messages.Add(message);
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to load messages", ex);
            }
        }

        private async Task HandleError(string message, Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"{message}: {ex}");

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (!_disposed)
                {
                    ErrorMessage = message;
                }
            });

            await _configService.AddLogEntryAsync("ERROR",
                $"{message}: {ex.Message}");
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
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
                    _smsService.OnSmsReceived -= HandleSmsReceived;
                    _refreshCancellation?.Cancel();
                    _refreshCancellation?.Dispose();
                    _processingLock?.Dispose();
                }
                _disposed = true;
            }
        }
    }
}