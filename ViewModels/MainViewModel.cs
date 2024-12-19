using SyncOne.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using SyncOne.Models;
using SmsMessage = SyncOne.Models.SmsMessage;
using SyncOne.Views;

namespace SyncOne.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly ISmsService _smsService;
        private readonly DatabaseService _databaseService;
        private readonly ApiService _apiService;
        private ObservableCollection<SmsMessage> _messages;
        private INavigation _navigation;
        private readonly IServiceProvider _serviceProvider;

        public Command OpenSettingsCommand { get; private set; }


        public ObservableCollection<SmsMessage> Messages
        {
            get => _messages;
            set
            {
                _messages = value;
                OnPropertyChanged();
            }
        }
        public MainViewModel(
      
       IServiceProvider serviceProvider,
       ISmsService smsService,
       DatabaseService databaseService,
       ApiService apiService
     )
        {
       
            _serviceProvider = serviceProvider;
            _smsService = smsService;
            _databaseService = databaseService;
            _apiService = apiService;

            _messages = new ObservableCollection<SmsMessage>();
            _smsService.OnSmsReceived += async (sender, message) =>
            {
                await ProcessIncomingSmsAsync(message);
            };
        }
        private async Task ProcessIncomingSmsAsync(SmsMessage message)
        {
            await _databaseService.SaveMessageAsync(message);
            Messages.Add(message);

            var response = await _apiService.ProcessMessageAsync(message.Body);
            message.Response = response;
            message.IsProcessed = true;
            message.ProcessedAt = DateTime.UtcNow;

            await _databaseService.SaveMessageAsync(message);
            await _smsService.SendSmsAsync(message.From, response);
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void Initialize(INavigation navigation)
        {
            _navigation = navigation;
            OpenSettingsCommand = new Command(async () => await OpenSettingsAsync());

            // Load existing messages
            LoadMessagesAsync().ConfigureAwait(false);
        }

        private async Task OpenSettingsAsync()
        {
            var configPage = _serviceProvider.GetRequiredService<Views.ConfigurationPage>();
            await _navigation.PushAsync(configPage);
        }

        private async Task LoadMessagesAsync()
        {
            try
            {
                // Load messages from database
                var messages = await _databaseService.GetMessagesAsync();
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    Messages.Clear();
                    foreach (var message in messages)
                    {
                        Messages.Add(message);
                    }
                });
            }
            catch (Exception ex)
            {
                // Handle or log error
                System.Diagnostics.Debug.WriteLine($"Error loading messages: {ex.Message}");
            }
        }

    }
}
