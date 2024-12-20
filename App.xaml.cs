namespace SyncOne
{
    public partial class App : Application
    {
        private readonly IServiceProvider _serviceProvider;

        public App(IServiceProvider serviceProvider)
        {
            InitializeComponent();
            _serviceProvider = serviceProvider;

            // Get the main page and create navigation page with styling
            var mainPage = _serviceProvider.GetRequiredService<MainPage>();
            MainPage = new NavigationPage(mainPage)
            {
                BarBackgroundColor = Colors.Purple, // Match your UI
                BarTextColor = Colors.White
            };

            // Initialize async stuff
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await InitializeAsync();
            });
        }

        private async Task InitializeAsync()
        {
            try
            {
                var configService = _serviceProvider.GetRequiredService<Services.ConfigurationService>();
                await configService.InitializeAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Initialization error: {ex.Message}");

                // Show error to user on main thread
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await Current.MainPage.DisplayAlert(
                        "Initialization Error",
                        "There was a problem starting the application. Please try again.",
                        "OK"
                    );
                });
            }
        }
    }
}