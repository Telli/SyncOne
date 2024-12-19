using SyncOne.Models;
using SyncOne.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace SyncOne.ViewModels
{
    public class ConfigurationViewModel : INotifyPropertyChanged
    {
        private readonly ConfigurationService _configService;
        private AppConfig _config;
        private ObservableCollection<PhoneNumberFilter> _allowedNumbers;
        private ObservableCollection<PhoneNumberFilter> _blockedNumbers;
        private string _newPhoneNumber;

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
                OnPropertyChanged();
            }
        }

        public ICommand SaveConfigCommand { get; }
        public ICommand AddAllowedNumberCommand { get; }
        public ICommand AddBlockedNumberCommand { get; }
        public ICommand RemoveNumberCommand { get; }

        public ConfigurationViewModel(ConfigurationService configService)
        {
            _configService = configService;
            AllowedNumbers = new ObservableCollection<PhoneNumberFilter>();
            BlockedNumbers = new ObservableCollection<PhoneNumberFilter>();

            SaveConfigCommand = new Command(async () => await SaveConfigAsync());
            AddAllowedNumberCommand = new Command(async () => await AddPhoneNumberAsync(true));
            AddBlockedNumberCommand = new Command(async () => await AddPhoneNumberAsync(false));
            RemoveNumberCommand = new Command<PhoneNumberFilter>(async (filter) => await RemoveNumberAsync(filter));

            LoadDataAsync();
        }

        private async Task LoadDataAsync()
        {
            Config = await _configService.GetConfigAsync();
            var allowed = await _configService.GetPhoneNumberFiltersAsync(true);
            var blocked = await _configService.GetPhoneNumberFiltersAsync(false);

            AllowedNumbers = new ObservableCollection<PhoneNumberFilter>(allowed);
            BlockedNumbers = new ObservableCollection<PhoneNumberFilter>(blocked);
        }

        private async Task SaveConfigAsync()
        {
            await _configService.SaveConfigAsync(Config);
        }

        private async Task AddPhoneNumberAsync(bool isAllowed)
        {
            if (string.IsNullOrWhiteSpace(NewPhoneNumber)) return;

            await _configService.AddPhoneNumberFilterAsync(NewPhoneNumber, isAllowed);
            await LoadDataAsync();
            NewPhoneNumber = string.Empty;
        }

        private async Task RemoveNumberAsync(PhoneNumberFilter filter)
        {
            await _configService.RemovePhoneNumberFilterAsync(filter.Id);
            await LoadDataAsync();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
