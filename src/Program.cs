using System;
using System.IO;
using System.Threading;
using System.Windows;

namespace DUN;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        if (args.Length == 2 && args[0] == "--preview")
        {
            var preview = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            preview.Startup += (_, _) => Preview.Render(Path.GetFullPath(args[1]), preview.Shutdown);
            preview.Run();
            return;
        }
        if (args.Length == 2 && args[0] == "--shots")
        {
            var shots = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            shots.Startup += (_, _) => Shots.Run(Path.GetFullPath(args[1]), shots.Shutdown);
            shots.Run();
            return;
        }

        using var mutex = new Mutex(true, @"Local\DUN.SingleInstance", out bool first);
        if (!first) return;

        // The app used to be called Hangline; carry its history over once.
        var old = Path.Combine(Path.GetDirectoryName(Settings.DataDir)!, "Hangline");
        if (Directory.Exists(old) && !Directory.Exists(Settings.DataDir))
        {
            try { Directory.Move(old, Settings.DataDir); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }

        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var controller = new AppController(app);
        app.Startup += (_, _) => controller.Start();
        app.Exit += (_, _) => controller.Dispose();
        app.DispatcherUnhandledException += (_, e) =>
        {
            Log(e.Exception);
            e.Handled = true;
        };
        app.Run();
    }

    static void Log(Exception e)
    {
        try
        {
            Directory.CreateDirectory(Settings.DataDir);
            File.AppendAllText(Path.Combine(Settings.DataDir, "error.log"), $"[{DateTime.Now:s}] {e}\n\n");
        }
        catch (IOException) { }
    }
}
