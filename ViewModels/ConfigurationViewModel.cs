using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using SyncOne.Models;
using SyncOne.Services;
#if ANDROID
using Android.Content;
using SyncOne.Platforms.Android.Services;
#endif

namespace SyncOne.ViewModels
{
    public class ConfigurationViewModel : INotifyPropertyChanged
    {
        private readonly ConfigurationService _configService;
        private readonly ILogger<ConfigurationViewModel> _logger;
        private AppConfig _config;
        private ObservableCollection<PhoneNumberFilter> _allowedNumbers;
        private ObservableCollection<PhoneNumberFilter> _blockedNumbers;
        private string _newPhoneNumber;
        private bool _isLoading;
        private bool _isSaving;
        private bool _isAddingNumber;
        private string _phoneNumberError;
        private string _apiUrlError;
        private bool _isServiceRunning;
        private string _serviceStatusMessage;

        public AppConfig Config
        {
            get => _config;
            set
            {
                _config = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<PhoneNumberFilter> AllowedNumbers
        {
            get => _allowedNumbers;
            set
            {
                _allowedNumbers = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<PhoneNumberFilter> BlockedNumbers
        {
            get => _blockedNumbers;
            set
            {
                _blockedNumbers = value;
                OnPropertyChanged();
            }
        }

        public string NewPhoneNumber
        {
            get => _newPhoneNumber;
            set
            {
                _newPhoneNumber = value;
                PhoneNumberError = null; // Clear error when input changes
                OnPropertyChanged();
            }
        }

        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                _isLoading = value;
                OnPropertyChanged();
            }
        }

        public bool IsSaving
        {
            get => _isSaving;
            set
            {
                _isSaving = value;
                OnPropertyChanged();
            }
        }

        public bool IsAddingNumber
        {
            get => _isAddingNumber;
            set
            {
                _isAddingNumber = value;
                OnPropertyChanged();
            }
        }

        public string PhoneNumberError
        {
            get => _phoneNumberError;
            set
            {
                _phoneNumberError = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasPhoneNumberError));
            }
        }

        public bool HasPhoneNumberError => !string.IsNullOrEmpty(PhoneNumberError);

        public string ApiUrlError
        {
            get => _apiUrlError;
            set
            {
                _apiUrlError = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasApiUrlError));
            }
        }

        public bool HasApiUrlError => !string.IsNullOrEmpty(ApiUrlError);

        public bool IsServiceRunning
        {
            get => _isServiceRunning;
            set
            {
                _isServiceRunning = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ServiceStatusMessage));
            }
        }

        public string ServiceStatusMessage
        {
            get => _isServiceRunning ? "Service is running" : "Service is stopped";
            set
            {
                _serviceStatusMessage = value;
                OnPropertyChanged();
            }
        }

        public ICommand SaveConfigCommand { get; }
        public ICommand AddAllowedNumberCommand { get; }
        public ICommand AddBlockedNumberCommand { get; }
        public ICommand RemoveNumberCommand { get; }
        public ICommand EnableBackgroundServiceCommand { get; }
        public ICommand DisableBackgroundServiceCommand { get; }

        public ConfigurationViewModel(ConfigurationService configService, ILogger<ConfigurationViewModel> logger)
        {
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            AllowedNumbers = new ObservableCollection<PhoneNumberFilter>();
            BlockedNumbers = new ObservableCollection<PhoneNumberFilter>();
            EnableBackgroundServiceCommand = new Command(EnableBackgroundService);
            DisableBackgroundServiceCommand = new Command(DisableBackgroundService);

            SaveConfigCommand = new Command(async () => await SaveConfigAsync());
            AddAllowedNumberCommand = new Command(async () => await AddPhoneNumberAsync(true));
            AddBlockedNumberCommand = new Command(async () => await AddPhoneNumberAsync(false));
            RemoveNumberCommand = new Command<PhoneNumberFilter>(async (filter) => await RemoveNumberAsync(filter));

            LoadDataAsync();
        }

        private bool ValidatePhoneNumber(string number)
        {
            if (string.IsNullOrWhiteSpace(number))
            {
                PhoneNumberError = "Phone number is required";
                return false;
            }

            // Basic phone number validation
            var regex = new Regex(@"^(\+?[\d\s-]{10,}|\*?\d{3,6}#?)$");
            if (!regex.IsMatch(number))
            {
                PhoneNumberError = "Invalid phone number format";
                return false;
            }

            PhoneNumberError = null;
            return true;
        }

        private void EnableBackgroundService()
        {
#if ANDROID
            try
            {
                var context = Platform.CurrentActivity.ApplicationContext;
                var intent = new Intent(context, typeof(BackgroundSmsService));
                if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.O)
                {
                    // Use StartForegroundService for Android O and above
                    context.StartForegroundService(intent);
                }
                else
                {
                    // Fallback to StartService for older versions
                    context.StartService(intent);
                }

                IsServiceRunning = true;
                ServiceStatusMessage = "Service started successfully";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start background service.");
                ServiceStatusMessage = $"Failed to start service: {ex.Message}";
            }
#endif
        }

        private void DisableBackgroundService()
        {
#if ANDROID
            try
            {
                var context = Platform.CurrentActivity.ApplicationContext;
                var intent = new Intent(context, typeof(BackgroundSmsService));
                context.StopService(intent);

                IsServiceRunning = false;
                ServiceStatusMessage = "Service stopped successfully";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to stop background service.");
                ServiceStatusMessage = $"Failed to stop service: {ex.Message}";
            }
#endif
        }

        private bool ValidateApiUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                ApiUrlError = "API URL is required";
                return false;
            }

            if (!Uri.TryCreate(url, UriKind.Absolute, out _))
            {
                ApiUrlError = "Invalid URL format";
                return false;
            }

            ApiUrlError = null;
            return true;
        }

        private async Task LoadDataAsync()
        {
            try
            {
                IsLoading = true;
                Config = await _configService.GetConfigAsync();
                var allowed = await _configService.GetPhoneNumberFiltersAsync(true);
                var blocked = await _configService.GetPhoneNumberFiltersAsync(false);

                AllowedNumbers = new ObservableCollection<PhoneNumberFilter>(allowed);
                BlockedNumbers = new ObservableCollection<PhoneNumberFilter>(blocked);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load configuration.");
                await Application.Current.MainPage.DisplayAlert("Error",
                    "Failed to load configuration: " + ex.Message, "OK");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task SaveConfigAsync()
        {
            if (!ValidateApiUrl(Config.ApiUrl))
                return;

            try
            {
                IsSaving = true;
                await _configService.SaveConfigAsync(Config);
                _logger.LogInformation("Configuration saved successfully.");
                await Application.Current.MainPage.DisplayAlert("Success",
                    "Configuration saved successfully", "OK");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save configuration.");
                await Application.Current.MainPage.DisplayAlert("Error",
                    "Failed to save configuration: " + ex.Message, "OK");
            }
            finally
            {
                IsSaving = false;
            }
        }

        private async Task AddPhoneNumberAsync(bool isAllowed)
        {
            if (!ValidatePhoneNumber(NewPhoneNumber))
                return;

            try
            {
                IsAddingNumber = true;
                await _configService.AddPhoneNumberFilterAsync(NewPhoneNumber, isAllowed);
                await LoadDataAsync();
                NewPhoneNumber = string.Empty;
                _logger.LogInformation($"Number added to {(isAllowed ? "allowlist" : "blocklist")}.");
                await Application.Current.MainPage.DisplayAlert("Success",
                    $"Number added to {(isAllowed ? "allowlist" : "blocklist")}", "OK");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to add number.");
                await Application.Current.MainPage.DisplayAlert("Error",
                    "Failed to add number: " + ex.Message, "OK");
            }
            finally
            {
                IsAddingNumber = false;
            }
        }

        private async Task RemoveNumberAsync(PhoneNumberFilter filter)
        {
            try
            {
                await _configService.RemovePhoneNumberFilterAsync(filter.Id);
                await LoadDataAsync();
                _logger.LogInformation("Number removed successfully.");
                await Application.Current.MainPage.DisplayAlert("Success",
                    "Number removed successfully", "OK");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to remove number.");
                await Application.Current.MainPage.DisplayAlert("Error",
                    "Failed to remove number: " + ex.Message, "OK");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}