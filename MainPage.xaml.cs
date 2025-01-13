using Microsoft.Extensions.Logging;
using SyncOne.ViewModels;
using SyncOne.Views;
using System;
using System.ComponentModel;

namespace SyncOne
{
    public partial class MainPage : ContentPage, IDisposable
    {
        private readonly MainViewModel _viewModel;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<MainPage> _logger;
        private bool _disposed;

        public MainPage(MainViewModel viewModel, IServiceProvider serviceProvider, ILogger<MainPage> logger)
        {
            InitializeComponent();

            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            BindingContext = _viewModel;
        }

        private async void OnSettingsClicked(object sender, EventArgs e)
        {
            try
            {
                var configPage = _serviceProvider.GetRequiredService<ConfigurationPage>();
                await Navigation.PushAsync(configPage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to navigate to settings page.");
                await DisplayAlert("Error", "Could not open settings: " + ex.Message, "OK");
            }
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            _logger.LogInformation("MainPage is appearing.");
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            _logger.LogInformation("MainPage is disappearing.");
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
                    // Unsubscribe from events or clean up resources if needed
                }
                _disposed = true;
            }
        }

        ~MainPage()
        {
            Dispose(false);
        }
    }
}