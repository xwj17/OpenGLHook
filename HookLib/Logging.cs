using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace HookLib
{
    internal static class Logging
    {
        private static readonly object _lock = new object();
        private static string _log = Path.Combine(Path.GetTempPath(), "ManagedD3D12Hook.log");
        private static bool _fileOk = true;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern void OutputDebugString(string lpOutputString);

        public static void Init()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_log) ?? ".");
                File.AppendAllText(_log, $"==== Start {DateTime.Now:yyyy-MM-dd HH:mm:ss} ====\n");
            }
            catch
            {
                _fileOk = false;
            }
        }

        private static void Write(string lv, string msg)
        {
            var line = $"[{DateTime.Now:HH:mm:ss.fff}][{lv}][T{Thread.CurrentThread.ManagedThreadId}] {msg}\n";
            lock (_lock)
            {
                if (_fileOk)
                {
                    try { File.AppendAllText(_log, line, Encoding.UTF8); }
                    catch { _fileOk = false; }
                }
            }
            OutputDebugString(line);
        }

        public static void Info(string m) => Write("INF", m);
        public static void Error(string m) => Write("ERR", m);
        public static void Warn(string m) => Write("WRN", m);
        public static void Exception(string where, Exception ex) => Write("EXC", $"{where}: {ex}");
    }
}