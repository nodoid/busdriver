using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using BusDriver.Game;
using Microsoft.Xna.Framework;

namespace BusDriver.Android;

/// <summary>Android entry point. Full screen, landscape, no title bar.</summary>
[Activity(
    Label = "Bus Driver",
    MainLauncher = true,
    Icon = "@mipmap/appicon",
    Theme = "@android:style/Theme.NoTitleBar.Fullscreen",
    ScreenOrientation = ScreenOrientation.SensorLandscape,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize |
                           ConfigChanges.Keyboard | ConfigChanges.KeyboardHidden)]
public class MainActivity : AndroidGameActivity
{
    private BusDriverGame? _game;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        _game = new BusDriverGame();
        _game.Window.AllowUserResizing = false;

        var view = (View)_game.Services.GetService(typeof(View))!;
        view.SystemUiVisibility = StatusBarVisibility.Hidden;
        SetContentView(view);
        _game.Run();
    }

    protected override void OnPause()
    {
        base.OnPause();
    }

    protected override void OnDestroy()
    {
        _game?.Dispose();
        _game = null;
        base.OnDestroy();
    }
}
