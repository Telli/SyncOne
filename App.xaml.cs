using Microsoft.Maui.Controls;
using Microsoft.Extensions.DependencyInjection;

#if ANDROID
using Android.Content;
using Android.App;
using Microsoft.Maui.Controls.PlatformConfiguration.AndroidSpecific;
using SyncOne.Platforms.Android.Services;
using Application = Android.App.Application;
#endif

namespace SyncOne
{
    public partial class App : Microsoft.Maui.Controls.Application
    {
        private readonly IServiceProvider _serviceProvider;

        public App(IServiceProvider serviceProvider)
        {
            InitializeComponent();
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
            _serviceProvider = serviceProvider;

            var mainPage = _serviceProvider.GetRequiredService<MainPage>();
            MainPage = new NavigationPage(mainPage)
            {
                BarBackgroundColor = Colors.Purple,
                BarTextColor = Colors.White
            };

            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await InitializeAsync();
            });
        }

        protected override void OnSleep()
        {
            base.OnSleep();

#if ANDROID
            if (OperatingSystem.IsAndroid())
            {
                var context = Android.App.Application.Context;
                var intent = new Intent(context, typeof(BackgroundSmsService));
                context.StartForegroundService(intent);
            }
#endif
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

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var exception = e.ExceptionObject as Exception;
            // Log the exception
            System.Diagnostics.Debug.WriteLine($"Unhandled exception: {exception}");
        }

        private void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            // Log the exception
            System.Diagnostics.Debug.WriteLine($"Unobserved Task exception: {e.Exception}");
            e.SetObserved(); // Prevent the app from crashing
        }
    }
}