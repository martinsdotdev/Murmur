using System;
using System.IO;
using System.Threading;

namespace Murmur.Services;

/// <summary>Append-only log at <c>%LOCALAPPDATA%\Murmur\debug.log</c>. Thread-safe.</summary>
internal static class DiagnosticLog
{
    private static readonly string _path = InitPath();
    private static readonly object _lock = new();

    private static string InitPath()
    {
        var dir = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Murmur");
        Directory.CreateDirectory(dir);
        var path = System.IO.Path.Combine(dir, "debug.log");
        try
        {
            File.WriteAllText(path,
                $"=== Murmur log started {DateTime.Now:yyyy-MM-dd HH:mm:ss} (PID {Environment.ProcessId}) ===\n");
        }
        catch { }
        return path;
    }

    public static string FilePath => _path;

    public static void Log(string msg)
    {
        try
        {
            lock (_lock)
            {
                File.AppendAllText(_path,
                    $"[{DateTime.Now:HH:mm:ss.fff} T{Thread.CurrentThread.ManagedThreadId}] {msg}\n");
            }
        }
        catch { }
    }
}
