using Microsoft.Extensions.Logging;
using SyncOne.Services;
using SyncOne.ViewModels;
using SQLite;
#if ANDROID
using SyncOne.Platforms.Android.Services;
#endif

namespace SyncOne
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                   // fonts.AddFont("MaterialIcons-Regular.ttf", "MaterialIcons");
                });

            // Setup SQLite
            string dbPath = Path.Combine(FileSystem.AppDataDirectory, "syncone.db3");
            builder.Services.AddSingleton(new SQLiteAsyncConnection(dbPath));

#if ANDROID
            builder.Logging.AddDebug();
            builder.Services.AddSingleton<ISmsService, AndroidSmsService>();
#endif
            // Register core services
            builder.Services.AddSingleton<DatabaseService>();
            builder.Services.AddSingleton<ConfigurationService>();

            builder.Services.AddSingleton<ApiService>();
            // Register ViewModels
            builder.Services.AddTransient<MainViewModel>(serviceProvider =>
                new MainViewModel(
                    serviceProvider,
                    serviceProvider.GetRequiredService<ISmsService>(),
                    serviceProvider.GetRequiredService<DatabaseService>(),
                    serviceProvider.GetRequiredService<ApiService>(),
                    serviceProvider.GetRequiredService<ConfigurationService>()
                ));

            builder.Services.AddTransient<ConfigurationViewModel>();

            // Register Pages
            builder.Services.AddTransient<Views.ConfigurationPage>();
            builder.Services.AddTransient<MainPage>();

            return builder.Build();
        }
    }
}