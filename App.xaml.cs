using Microsoft.Maui.Controls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using SyncOne.Services;

#if ANDROID
using Android.Content;
using Android.App;
using SyncOne.Platforms.Android.Services;
using Application = Android.App.Application;
#endif

namespace SyncOne
{
    public partial class App : Microsoft.Maui.Controls.Application, IDisposable
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<App> _logger;
        private bool _disposed;

        public App(IServiceProvider serviceProvider, ILogger<App> logger)
        {
            InitializeComponent();

            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

            var mainPage = _serviceProvider.GetRequiredService<MainPage>();
            MainPage = new NavigationPage(mainPage)
            {
                BarBackgroundColor = Colors.Purple,
                BarTextColor = Colors.White
            };

            MainThread.InvokeOnMainThreadAsync(async () =>
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
                var context = Application.Context;
                var intent = new Intent(context, typeof(BackgroundSmsService));
                context.StartForegroundService(intent);
            }
#endif
        }

        protected override void OnResume()
        {
            base.OnResume();

#if ANDROID
            if (OperatingSystem.IsAndroid())
            {
                var context = Application.Context;
                var intent = new Intent(context, typeof(BackgroundSmsService));
                context.StopService(intent);
            }
#endif
        }

        private async Task InitializeAsync()
        {
            try
            {
                var configService = _serviceProvider.GetRequiredService<ConfigurationService>();
                await configService.InitializeAsync();
                _logger.LogInformation("Application initialized successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize application.");

                try
                {
                    await MainThread.InvokeOnMainThreadAsync(async () =>
                    {
                        await Current.MainPage.DisplayAlert(
                            "Initialization Error",
                            "There was a problem starting the application. Please try again.",
                            "OK"
                        );
                    });
                }
                catch (Exception alertEx)
                {
                    _logger.LogError(alertEx, "Failed to display initialization error alert.");
                }
            }
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var exception = e.ExceptionObject as Exception;
            _logger.LogError(exception, "Unhandled exception occurred. IsTerminating: {IsTerminating}", e.IsTerminating);
        }

        private void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            _logger.LogError(e.Exception, "Unobserved Task exception occurred.");
            e.SetObserved(); // Prevent the app from crashing
        }

        public void Dispose()
        {
            if (_disposed) return;
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Unsubscribe from events
                    AppDomain.CurrentDomain.UnhandledException -= CurrentDomain_UnhandledException;
                    TaskScheduler.UnobservedTaskException -= TaskScheduler_UnobservedTaskException;
                }
                _disposed = true;
            }
        }
    }
}