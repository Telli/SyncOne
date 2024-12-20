using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using SyncOne.Services;
using SyncOne.Models;
using SmsMessage = SyncOne.Models.SmsMessage;

namespace SyncOne.ViewModels;
public class MainViewModel : INotifyPropertyChanged
{
    private readonly ISmsService _smsService;
    private readonly DatabaseService _databaseService;
    private readonly ApiService _apiService;
    private ObservableCollection<SyncOne.Models.SmsMessage> _messages;
    private INavigation _navigation;
    private readonly IServiceProvider _serviceProvider;
    private bool _isRefreshing;
    private bool _hasError;
    private string _errorMessage;
    private ConfigurationService _configService;

    public Command OpenSettingsCommand { get; private set; }
    public Command RefreshCommand { get; private set; }

    public ObservableCollection<Models.SmsMessage> Messages
    {
        get => _messages;
        set
        {
            _messages = value;
            OnPropertyChanged();
        }
    }

    public bool IsRefreshing
    {
        get => _isRefreshing;
        set
        {
            _isRefreshing = value;
            OnPropertyChanged();
        }
    }

    public bool HasError
    {
        get => _hasError;
        set
        {
            _hasError = value;
            OnPropertyChanged();
        }
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        set
        {
            _errorMessage = value;
            OnPropertyChanged();
            HasError = !string.IsNullOrEmpty(value);
        }
    }

    public MainViewModel(
        IServiceProvider serviceProvider,
        ISmsService smsService,
        DatabaseService databaseService,
        ApiService apiService, ConfigurationService configurationService)
    {
        _serviceProvider = serviceProvider;
        _smsService = smsService;
        _databaseService = databaseService;
        _apiService = apiService;
        _messages = new ObservableCollection<SmsMessage>();
        _configService = configurationService;

        InitializeDatabaseAsync().ConfigureAwait(false);


        _smsService.OnSmsReceived += async (sender, message) =>
        {
            await ProcessIncomingSmsAsync(message);
        };

        RefreshCommand = new Command(async () => await RefreshMessagesAsync());
    }

    private async Task InitializeDatabaseAsync()
    {
        try
        {
            await _databaseService.InitializeAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = "Failed to initialize database";
            System.Diagnostics.Debug.WriteLine($"Database initialization error: {ex.Message}");
        }
    }

    private async Task RefreshMessagesAsync()
    {
        try
        {
            IsRefreshing = true;
            await LoadMessagesAsync();
            ErrorMessage = null;
        }
        catch (Exception ex)
        {
            ErrorMessage = "Failed to refresh messages";
            System.Diagnostics.Debug.WriteLine($"Error refreshing: {ex.Message}");
        }
        finally
        {
            IsRefreshing = false;
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

            if (Messages != null)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    Messages.Insert(0, message);
                });
            }

            var response = await _apiService.ProcessMessageAsync(message.From, message.Body);

            // Try to send SMS with retries
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
                        await Task.Delay(TimeSpan.FromSeconds(2 * retryCount)); // Exponential backoff
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

            if (Messages != null)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    var existingMessage = Messages.FirstOrDefault(m => m.Id == message.Id);
                    if (existingMessage != null)
                    {
                        var index = Messages.IndexOf(existingMessage);
                        Messages[index] = message;
                    }
                });
            }

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
        }
    }

    private async Task LoadMessagesAsync()
    {
        try
        {
            await _databaseService.InitializeAsync();
            var messages = await _databaseService.GetMessagesAsync();
            MainThread.BeginInvokeOnMainThread(() =>
            {
                Messages.Clear();
                foreach (var message in messages.OrderByDescending(m => m.ReceivedAt))
                {
                    Messages.Add(message);
                }
            });
            ErrorMessage = null;
        }
        catch (Exception ex)
        {
            ErrorMessage = "Failed to load messages";
            System.Diagnostics.Debug.WriteLine($"Error loading messages: {ex.Message}");
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}