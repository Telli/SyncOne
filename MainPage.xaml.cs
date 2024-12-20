using SyncOne.ViewModels;

namespace SyncOne
{
    public partial class MainPage : ContentPage
    {
        private readonly MainViewModel _viewModel;
        private readonly IServiceProvider _serviceProvider;

        public MainPage(MainViewModel viewModel, IServiceProvider serviceProvider)
        {
            InitializeComponent();
            _viewModel = viewModel;
            _serviceProvider = serviceProvider;
            BindingContext = _viewModel;
        }

        private async void OnSettingsClicked(object sender, EventArgs e)
        {
            try
            {
                var configPage = _serviceProvider.GetRequiredService<Views.ConfigurationPage>();
                await Navigation.PushAsync(configPage);
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", "Could not open settings: " + ex.Message, "OK");
            }
        }


        protected override void OnAppearing()
        {
            base.OnAppearing();
        }
    }

}
