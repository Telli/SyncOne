using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using SyncOne.Models;
using SyncOne.Services;
using System.Threading;
using Microsoft.Extensions.Logging;
using SmsMessage = SyncOne.Models.SmsMessage;
using Microsoft.Maui.ApplicationModel;

namespace SyncOne.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly ISmsService _smsService;
        private readonly DatabaseService _databaseService;
        private readonly ApiService _apiService;
        private readonly ConfigurationService _configService;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<MainViewModel> _logger;
        private readonly SemaphoreSlim _processingLock = new(1, 1);
        private bool _disposed;

        private ObservableCollection<SmsMessage> _messages = new();
        private ObservableCollection<SmsMessage> _filteredMessages = new();
        private string _searchQuery;
        private bool _isRefreshing;
        private bool _hasError;
        private string _errorMessage;
        private CancellationTokenSource _refreshCancellation;

        public Command OpenSettingsCommand { get; } // Assuming this is initialized elsewhere
        public Command RefreshCommand { get; }

        public ObservableCollection<SmsMessage> Messages
        {
            get => _messages;
            private set
            {
                if (_messages != value)
                {
                    if (_messages != null)
                    {
                        _messages.CollectionChanged -= Messages_CollectionChanged;
                    }
                    _messages = value ?? new ObservableCollection<SmsMessage>();
                    _messages.CollectionChanged += Messages_CollectionChanged;
                    OnPropertyChanged();
                    FilterMessages();
                }
            }
        }

        private void Messages_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            FilterMessages();
        }

        public ObservableCollection<SmsMessage> FilteredMessages
        {
            get => _filteredMessages;
            private set
            {
                if (_filteredMessages != value)
                {
                    _filteredMessages = value;
                    OnPropertyChanged();
                }
            }
        }

        public string SearchQuery
        {
            get => _searchQuery;
            set
            {
                if (_searchQuery != value)
                {
                    _searchQuery = value;
                    OnPropertyChanged();
                    FilterMessages();
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
                    RefreshCommand.ChangeCanExecute();
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

            _logger = _serviceProvider.GetService<ILogger<MainViewModel>>()
                ?? throw new InvalidOperationException("ILogger<MainViewModel> not found.");

            _refreshCancellation = new CancellationTokenSource();

            RefreshCommand = new Command(
                async () => await RefreshMessagesAsync(),
                () => !IsRefreshing
            );

            // Initialize on background thread
            Task.Run(InitializeAsync).ContinueWith(
                t => _logger.LogError(t.Exception, "Initialization failed"),
                TaskContinuationOptions.OnlyOnFaulted
            );

            _smsService.OnSmsReceived += HandleSmsReceived;
        }

        private async Task InitializeAsync()
        {
            try
            {
                await InitializeDatabaseAsync();
                await LoadMessagesAsync();
            }
            catch (Exception ex)
            {
                await HandleError("Initialization failed", ex);
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
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(_refreshCancellation.Token);
                    await ProcessIncomingSmsAsync(message, cts.Token);
                }
                catch (Exception ex)
                {
                    await HandleError("Failed to process incoming message", ex);
                }
            }).ContinueWith(
                t => _logger.LogError(t.Exception, "Unhandled error in SMS processing"),
                TaskContinuationOptions.OnlyOnFaulted
            );
        }

        private async Task RefreshMessagesAsync()
        {
            if (IsRefreshing)
            {
                _logger.LogInformation("Refresh already in progress, skipping");
                return;
            }

            try
            {
                ErrorMessage = null;

                var cts = new CancellationTokenSource();
                var previousCts = Interlocked.Exchange(ref _refreshCancellation, cts);
                previousCts?.Cancel();
                previousCts?.Dispose();

                IsRefreshing = true;
                _logger.LogInformation("Starting refresh operation");

                await InitializeDatabaseAsync();

                await LoadMessagesAsync(); // Directly await the async method

                _logger.LogInformation("Refresh completed successfully");
                ErrorMessage = null;
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Refresh operation was cancelled");
            }
            catch (TimeoutException)
            {
                var message = "Refresh operation timed out";
                _logger.LogWarning(message);
                await HandleError(message, new TimeoutException("The operation timed out"));
            }
            catch (Exception ex)
            {
                var message = "Failed to refresh messages";
                _logger.LogError(ex, message);
                await HandleError(message, ex);
            }
            finally
            {
                IsRefreshing = false;
            }
        }

        private async Task LoadMessagesAsync()
        {
            try
            {
                _logger.LogInformation("Loading messages from database");

                var messages = await _databaseService.GetMessagesAsync();
                if (messages == null)
                {
                    throw new InvalidOperationException("Database returned null messages collection");
                }

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    if (_disposed) return;

                    var orderedMessages = messages
                        .OrderByDescending(m => m.ReceivedAt)
                        .ToList();

                    _logger.LogInformation($"Loading {orderedMessages.Count} messages into view");

                    // Clear the existing collection and add new items
                    Messages.Clear();
                    foreach (var message in orderedMessages)
                    {
                        Messages.Add(message);
                    }

                    FilterMessages();
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load messages");
                throw new InvalidOperationException("Failed to load messages", ex);
            }
        }

        private void FilterMessages()
        {
            if (string.IsNullOrWhiteSpace(SearchQuery))
            {
                // If no search query, show all messages
                FilteredMessages = new ObservableCollection<SmsMessage>(Messages);
            }
            else
            {
                // Filter messages based on the search query
                var filtered = Messages
                    .Where(m => m.From.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ||
                                m.Body.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                FilteredMessages = new ObservableCollection<SmsMessage>(filtered);
            }
            OnPropertyChanged(nameof(FilteredMessages));
        }

        private async Task ProcessIncomingSmsAsync(SmsMessage message, CancellationToken cancellationToken)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));

            await _processingLock.WaitAsync(cancellationToken);
            try
            {
                if (!await _configService.IsPhoneNumberAllowedAsync(message.From))
                {
                    await _configService.AddLogEntryAsync("SMS_FILTERED",
                        $"Message from {message.From} was filtered out");
                    return;
                }
                if (message.IsProcessing || message.IsProcessed)
                {
                    _logger.LogInformation("Skipping message {MessageId} (already being processed or processed)", message.Id);
                    return;
                }

                message.IsProcessing = true;
                await _databaseService.SaveMessageAsync(message);
                await AddOrUpdateMessageInCollectionAsync(message);

                var apiResponse = await ProcessMessageWithRetryAsync(message, cancellationToken);
                if (apiResponse == null)
                {
                    await HandleMessageError(message, new Exception("Failed to process message after retries"));
                    return;
                }

                var sendSuccess = await SendSmsWithRetryAsync(message.From, apiResponse.Response);
                await UpdateMessageStatusAsync(message, apiResponse, sendSuccess);
                await LogProcessingResultAsync(message, sendSuccess);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation($"Processing of SMS from {message.From} cancelled.");
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

        private async Task<ApiResponse> ProcessMessageWithRetryAsync(SmsMessage message, CancellationToken cancellationToken)
        {
            const int maxRetries = 3;
            Exception lastException = null;

            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    var response = await _apiService.ProcessMessageAsync(message.From, message.Body);
                    if (response != null)
                    {
                        return response;
                    }
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    if (i < maxRetries - 1)
                    {
                        await _configService.AddLogEntryAsync("API_RETRY",
                            $"Retrying API call for {message.From}, attempt {i + 2}/{maxRetries}");
                        await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, i)), cancellationToken);
                    }
                }
            }

            if (lastException != null)
            {
                _logger.LogError(lastException, "API processing failed after all retries");
            }
            return null;
        }

        private async Task<bool> SendSmsWithRetryAsync(string phoneNumber, string messageText)
        {
            if (string.IsNullOrEmpty(phoneNumber) || string.IsNullOrEmpty(messageText))
            {
                _logger.LogError("Invalid SMS parameters: phone number or message is empty");
                return false;
            }

            const int maxRetries = 3;
            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    // Ensure permission is granted before sending SMS
                    bool hasPermission = await MainThread.InvokeOnMainThreadAsync(async () =>
                    {
                        return await CheckAndRequestSmsPermissionAsync();
                    });

                    if (!hasPermission)
                    {
                        _logger.LogError("SMS permission not granted.");
                        return false;
                    }

                    var success = await _smsService.SendSmsAsync(phoneNumber, messageText);
                    if (success) return true;

                    if (i < maxRetries - 1)
                    {
                        var delay = TimeSpan.FromSeconds(Math.Pow(2, i));
                        await _configService.AddLogEntryAsync("SMS_RETRY",
                            $"Retrying SMS send to {phoneNumber}, attempt {i + 2}/{maxRetries}");
                        await Task.Delay(delay);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"SMS send attempt {i + 1} failed");
                    // Do not rethrow; log the error and return false
                    return false;
                }
            }
            return false;
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

        private async Task AddOrUpdateMessageInCollectionAsync(SmsMessage message)
        {
            if (_disposed || message == null) return;

            try
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    if (!_disposed && Messages != null)
                    {
                        var existingMessage = Messages.FirstOrDefault(m => m.Id == message.Id);
                        if (existingMessage != null)
                        {
                            // Update the existing message
                            var index = Messages.IndexOf(existingMessage);
                            Messages[index] = message; // This will notify the UI
                        }
                        else
                        {
                            // Add the new message
                            Messages.Insert(0, message); // This will notify the UI
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to add/update message in collection");
            }
        }

        private async Task UpdateMessageStatusAsync(SmsMessage message, ApiResponse apiResponse, bool sendSuccess)
        {
            if (message == null || apiResponse == null) return;

            try
            {
                message.Response = apiResponse.Response;
                message.IsProcessed = true;
                message.IsProcessing = false;
                message.ProcessedAt = DateTime.UtcNow;
                message.SendStatus = sendSuccess ? "Sent" : "Failed";

                await _databaseService.SaveMessageAsync(message);
                await AddOrUpdateMessageInCollectionAsync(message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update message status");
                throw; // Re-throw after logging
            }
        }

        private async Task LogProcessingResultAsync(SmsMessage message, bool sendSuccess)
        {
            if (message == null) return;

            try
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to log processing result");
            }
        }

        private async Task HandleMessageError(SmsMessage message, Exception ex)
        {
            if (message == null) return;

            try
            {
                message.IsProcessing = false;
                message.SendStatus = "Error";

                await _databaseService.SaveMessageAsync(message);
                await _configService.AddLogEntryAsync("SMS_ERROR",
                    $"Error processing message from {message.From}: {ex.Message}");

                await HandleError($"Error processing message from {message.From}", ex);
            }
            catch (Exception loggingEx)
            {
                _logger.LogError(loggingEx, "Failed to handle message error");
            }
        }

        private async Task HandleError(string message, Exception ex)
        {
            _logger.LogError(ex, message);

            try
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    if (!_disposed)
                    {
                        ErrorMessage = $"{message}: {ex.Message}";
                    }
                });

                await _configService.AddLogEntryAsync("ERROR", $"{message}: {ex.Message}");
            }
            catch (Exception loggingEx)
            {
                _logger.LogError(loggingEx, "Failed to handle error");
            }
        }

        private void CleanupCancellationTokens()
        {
            try
            {
                _refreshCancellation?.Cancel();
                _refreshCancellation?.Dispose();
                _refreshCancellation = null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up cancellation tokens");
            }
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
                    CleanupCancellationTokens();
                    _processingLock?.Dispose();
                    if (Messages != null)
                    {
                        Messages.CollectionChanged -= Messages_CollectionChanged;
                        Messages.Clear();
                    }
                }
                _disposed = true;
            }
        }
    }
}