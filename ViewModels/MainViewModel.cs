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
        ApiService apiService)
    {
        _serviceProvider = serviceProvider;
        _smsService = smsService;
        _databaseService = databaseService;
        _apiService = apiService;
        _messages = new ObservableCollection<SmsMessage>();

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
            message.IsProcessing = true;
            await _databaseService.InitializeAsync();
            await _databaseService.SaveMessageAsync(message);

            MainThread.BeginInvokeOnMainThread(() =>
            {
                Messages.Insert(0, message);
            });

            var response = await _apiService.ProcessMessageAsync(message.From, message.Body);

            message.Response = response;
            message.IsProcessed = true;
            message.IsProcessing = false;
            message.ProcessedAt = DateTime.UtcNow;

            await _databaseService.SaveMessageAsync(message);
            await _smsService.SendSmsAsync(message.From, response);

            ErrorMessage = null;
        }
        catch (Exception ex)
        {
            message.IsProcessing = false;
            ErrorMessage = "Failed to process message";
            System.Diagnostics.Debug.WriteLine($"Error processing message: {ex.Message}");
        }
    }

    public void Initialize(INavigation navigation)
    {
        _navigation = navigation;
        OpenSettingsCommand = new Command(async () => await OpenSettingsAsync());
        LoadMessagesAsync().ConfigureAwait(false);
    }

    private async Task OpenSettingsAsync()
    {
        if (_navigation == null)
        {
            System.Diagnostics.Debug.WriteLine("Navigation is null");
            return;
        }

        try
        {
            var configPage = _serviceProvider.GetRequiredService<Views.ConfigurationPage>();
            await _navigation.PushAsync(configPage);
            ErrorMessage = null;
        }
        catch (Exception ex)
        {
            ErrorMessage = "Failed to open settings";
            System.Diagnostics.Debug.WriteLine($"Error opening settings: {ex.Message}");
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