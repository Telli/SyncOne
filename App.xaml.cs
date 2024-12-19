namespace SyncOne
{
    public partial class App : Application
    {
        private readonly IServiceProvider _serviceProvider;

        public App(IServiceProvider serviceProvider)
        {
            InitializeComponent();
            _serviceProvider = serviceProvider;

            // Set MainPage immediately
            var mainPage = _serviceProvider.GetRequiredService<MainPage>();
            MainPage = new NavigationPage(mainPage);

            // Then initialize async stuff
            Task.Run(async () =>
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
                // Handle initialization error appropriately
            }
        }
    }
}
