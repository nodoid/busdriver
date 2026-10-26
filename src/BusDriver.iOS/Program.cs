using AVFoundation;
using BusDriver.Game;
using BusDriver.Game.Core;
using Foundation;
using UIKit;

namespace BusDriver.iOS;

/// <summary>
/// iOS entry point.  As of the iOS 27 SDK UIKit requires scene adoption;
/// this class is both the application delegate and the scene delegate.  The
/// MonoGame window already exists by the time the scene connects, so it only
/// needs its UIWindowScene assigning before the game starts.
/// </summary>
[Register("AppDelegate")]
public class Program : UIApplicationDelegate, IUIWindowSceneDelegate
{
    private static BusDriverGame? _game;

    private static void Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            StartupLog.Write($"Unhandled: {e.ExceptionObject}");
        UIApplication.Main(args, null, typeof(Program));
    }

    public override bool FinishedLaunching(UIApplication application, NSDictionary? launchOptions)
    {
        StartupLog.Write("FinishedLaunching");
        try
        {
            var session = AVAudioSession.SharedInstance();
            session.SetCategory(AVAudioSessionCategory.Ambient);
            session.SetActive(true);
            StartupLog.Write("audio session active");
        }
        catch (Exception ex)
        {
            StartupLog.Exception("Audio session setup failed", ex);
        }

        return true;
    }

    public override UISceneConfiguration GetConfiguration(
        UIApplication application,
        UISceneSession connectingSceneSession,
        UISceneConnectionOptions options)
    {
        StartupLog.Write("GetConfiguration");
        var configuration = new UISceneConfiguration(
            "Default Configuration",
            connectingSceneSession.Role);
        configuration.DelegateType = typeof(Program);
        return configuration;
    }

    [Export("scene:willConnectToSession:options:")]
    public void WillConnect(
        UIScene scene,
        UISceneSession session,
        UISceneConnectionOptions connectionOptions)
    {
        StartupLog.Write("WillConnect");
        if (scene is not UIWindowScene windowScene)
        {
            StartupLog.Write("WillConnect: not a UIWindowScene");
            return;
        }

        try
        {
            _game = new BusDriverGame();
            StartupLog.Write("game constructed");
            var window = _game.Services.GetService<UIWindow>();
            if (window is null)
            {
                StartupLog.Write("game window service missing");
            }
            else
            {
                window.WindowScene = windowScene;
                StartupLog.Write("window scene assigned");
                StartupLog.Write($"screen scale {UIScreen.MainScreen.Scale}");
            }

            LogViewGeometry("before run", window);
            NSTimer.CreateScheduledTimer(2.0, _ => LogViewGeometry("after 2s", window));

            _game.Run();
            StartupLog.Write("game.Run returned");
            InstallTouchOverlay(window);
        }
        catch (Exception ex)
        {
            StartupLog.Exception("game.Run failed", ex);
            throw;
        }
    }

    /// <summary>Puts a transparent touch view over the game surface.</summary>
    private static void InstallTouchOverlay(UIWindow? window)
    {
        try
        {
            var host = window?.RootViewController?.View;
            if (host is null)
            {
                StartupLog.Write("touch overlay: no root view yet");
                return;
            }

            var overlay = new TouchOverlay(host.Bounds)
            {
                AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight
            };
            host.AddSubview(overlay);
            PlatformPointer.InUse = true;
            StartupLog.Write($"touch overlay installed {overlay.Bounds.Width:0}x{overlay.Bounds.Height:0}");
        }
        catch (Exception ex)
        {
            StartupLog.Exception("touch overlay failed", ex);
        }
    }

    private static void LogViewGeometry(string when, UIWindow? window)
    {
        try
        {
            if (window is null)
            {
                return;
            }

            var view = window.RootViewController?.View;
            var layer = view?.Layer;
            StartupLog.Write(
                $"{when}: window {window.Bounds.Width:0}x{window.Bounds.Height:0} " +
                $"view {view?.Bounds.Width:0}x{view?.Bounds.Height:0} " +
                $"contentScale {view?.ContentScaleFactor:0.0} " +
                $"layerScale {layer?.ContentsScale:0.0}");
        }
        catch (Exception ex)
        {
            StartupLog.Exception($"{when}: geometry log failed", ex);
        }
    }

    public override void OnResignActivation(UIApplication application)
    {
    }

    public override void DidEnterBackground(UIApplication application)
    {
    }

    public override void WillEnterForeground(UIApplication application)
    {
    }
}
