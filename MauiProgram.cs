using Microsoft.Extensions.Logging;
using SyncOne.Services;
using SyncOne.ViewModels;
using SQLite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;
#if ANDROID
using SyncOne.Platforms.Android.Services;
using Android.Content;
#endif

namespace SyncOne
{
    public static class MauiProgram
    {

        public static IServiceProvider ServiceProvider { get; private set; }
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

            // Setup SQLite
            string dbPath = Path.Combine(FileSystem.AppDataDirectory, "syncone.db3");
            builder.Services.AddSingleton(new SQLiteAsyncConnection(dbPath));

#if ANDROID
            // Register Android-specific services
            builder.Logging.AddDebug();
            builder.Services.AddSingleton<Context>(_ => Platform.AppContext); // Register Android Context
            builder.Services.AddSingleton<ISmsService, AndroidSmsService>(); // Register AndroidSmsService
            builder.Services.AddSingleton<BackgroundSmsService>();
#endif

            // Register core services
            builder.Services.AddSingleton<DatabaseService>();
            builder.Services.AddSingleton<ConfigurationService>();
            builder.Services.AddSingleton<ApiService>();
   
            builder.Logging.AddDebug();

            // Register logging
            builder.Services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.AddDebug(); // Add debug logging for development
            });

            // Register Pages
            builder.Services.AddTransient<Views.ConfigurationPage>();
            builder.Services.AddTransient<MainPage>();

            // Register ViewModels
            builder.Services.AddTransient<MainViewModel>(serviceProvider =>
     new MainViewModel(
         serviceProvider, 
         serviceProvider.GetRequiredService<ISmsService>(),
         serviceProvider.GetRequiredService<DatabaseService>(),
         serviceProvider.GetRequiredService<ApiService>(),
         serviceProvider.GetRequiredService<ConfigurationService>()
     ));

            builder.Services.AddTransient<ConfigurationViewModel>(serviceProvider =>
                new ConfigurationViewModel(
                    serviceProvider.GetRequiredService<ConfigurationService>(),
                    serviceProvider.GetRequiredService<ILogger<ConfigurationViewModel>>()
                ));



            var app = builder.Build();

           
            ServiceProvider = app.Services;

            return app;
        }
    }
}