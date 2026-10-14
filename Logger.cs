using System;
using System.IO;
using System.Text;
using System.Collections.Concurrent;

namespace OverscanFix
{
    /// <summary>
    /// Simple thread‑safe logger that writes structured JSON lines to a log file.
    /// Intended for production diagnostics – timestamps, severity, message, and optional exception.
    /// </summary>
    public static class Logger
    {
        private static readonly string LogFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OverscanFix", "overscanfix.log");
        private static readonly BlockingCollection<string> Queue = new BlockingCollection<string>();
        private static readonly System.Threading.Tasks.Task WriterTask;

        static Logger()
        {
            // Ensure directory exists
            Directory.CreateDirectory(Path.GetDirectoryName(LogFilePath)!);
            // Start background writer
            WriterTask = System.Threading.Tasks.Task.Run(() =>
            {
                using var stream = new FileStream(LogFilePath, FileMode.Append, FileAccess.Write, FileShare.Read);
                using var writer = new StreamWriter(stream, Encoding.UTF8);
                foreach (var line in Queue.GetConsumingEnumerable())
                {
                    writer.WriteLine(line);
                    writer.Flush();
                }
            });
        }

        private static void Enqueue(string level, string message, Exception? ex = null)
        {
            var entry = new
            {
                timestamp = DateTime.UtcNow.ToString("o"),
                level,
                message,
                exception = ex?.ToString()
            };
            var json = System.Text.Json.JsonSerializer.Serialize(entry);
            Queue.Add(json);
        }

        public static void Info(string message) => Enqueue("INFO", message);
        public static void Warn(string message) => Enqueue("WARN", message);
        public static void Error(string message, Exception? ex = null) => Enqueue("ERROR", message, ex);
        public static void Debug(string message) => Enqueue("DEBUG", message);

        /// <summary>
        /// Dispose must be called on application shutdown to flush pending logs.
        /// </summary>
        public static void Shutdown()
        {
            Queue.CompleteAdding();
            WriterTask.Wait();
        }
    }
}
