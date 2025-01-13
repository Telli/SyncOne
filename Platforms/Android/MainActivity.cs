using Android;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Widget;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using SyncOne.Platforms.Android.Services;

namespace SyncOne.Platforms.Android
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

#if ANDROID
            // Check and request necessary permissions
            if (ContextCompat.CheckSelfPermission(this, Manifest.Permission.ReceiveSms) != (int)Permission.Granted)
            {
                ActivityCompat.RequestPermissions(this, new[] { Manifest.Permission.ReceiveSms }, 0);
            }
            else
            {
                // Start the background service as a foreground service
                StartBackgroundService();
            }
#endif
        }

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
        {
            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);

            if (requestCode == 0)
            {
                if (grantResults.Length > 0 && grantResults[0] == Permission.Granted)
                {
                    // Permission granted, start the service
                    StartBackgroundService();
                }
                else
                {
                    // Permission denied, show a message to the user
                    Toast.MakeText(this, "SMS permission is required to process messages.", ToastLength.Long).Show();
                }
            }
        }

        private void StartBackgroundService()
        {
            var intent = new Intent(this, typeof(BackgroundSmsService));
            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            {
                StartForegroundService(intent);
            }
            else
            {
                StartService(intent);
            }
        }
    }
}
