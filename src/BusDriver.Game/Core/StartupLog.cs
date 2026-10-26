using System.Text;

namespace BusDriver.Game.Core;

/// <summary>
/// Writes a short startup log into the app's Documents folder.  It is also
/// mirrored to the console, but on a physical iPhone the console is not always
/// available, so the file is the reliable copy when something goes wrong.
/// </summary>
public static class StartupLog
{
    private static readonly object Gate = new();

    public static string Path
    {
        get
        {
            var folder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            return System.IO.Path.Combine(folder, "busdriver-startup.log");
        }
    }

    public static void Write(string message)
    {
        var line = $"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}  {message}{Environment.NewLine}";
        try
        {
            lock (Gate)
            {
                File.AppendAllText(Path, line, Encoding.UTF8);
            }
        }
        catch
        {
            // Logging must never be the reason the game fails.
        }

        try
        {
            Console.Write(line);
            Console.Out.Flush();
        }
        catch
        {
        }
    }

    public static void Exception(string context, Exception ex)
    {
        Write($"{context}: {ex.GetType().Name}: {ex.Message}");
        Write(ex.StackTrace ?? string.Empty);
        if (ex.InnerException is not null)
        {
            Write($"inner: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
            Write(ex.InnerException.StackTrace ?? string.Empty);
        }
    }
}
