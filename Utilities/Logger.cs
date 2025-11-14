
using System;
using System.IO;
using System.Linq;

namespace EOTReminder.Utilities
{
    public enum LogLevel
    {
        Info,
        Warning,
        Error
    }

    public static class Logger
    {
        private static readonly string LogDirectory = @"C:\EOTReminderLogs";
        private static readonly string LogFileName = "app.log";
        private static readonly long MaxFileSize = 1 * 1024 * 1024; // 1 MB (adjust as needed)

        // Static constructor to ensure log directory exists when Logger is first accessed
        static Logger()
        {
            EnsureLogDirectoryExists();
        }

        // Ensures the log directory exists, creating it if necessary.
        private static void EnsureLogDirectoryExists()
        {
            if (!Directory.Exists(LogDirectory))
            {
                try
                {
                    Directory.CreateDirectory(LogDirectory);
                    System.Diagnostics.Debug.WriteLine($"Log directory created: {LogDirectory}");
                }
                catch (Exception ex)
                {
                    // Fallback: if cannot create log directory, write to debug output
                    System.Diagnostics.Debug.WriteLine($"ERROR: Could not create log directory {LogDirectory}. Logging will be limited to debug output. Exception: {ex.Message}");
                }
            }
        }

        // Logs an informational message.
        public static void LogInfo(string message)
        {
            Log(message, LogLevel.Info);
        }

        // Logs a warning message.
        public static void LogWarning(string message)
        {
            Log(message, LogLevel.Warning);
        }

        // Logs an error message, optionally including an exception.
        public static void LogError(string message, Exception ex = null)
        {
            string fullMessage = message;
            if (ex != null)
            {
                fullMessage += $" Exception: {ex.Message}";
                if (ex.StackTrace != null)
                {
                    fullMessage += $"\nStackTrace: {ex.StackTrace}";
                }
            }
            Log(fullMessage, LogLevel.Error);
        }

        // Core logging method that writes to the file and handles rotation.
        private static void Log(string message, LogLevel level)
        {
            string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level.ToString().ToUpper()}] {message}";

            // Always write to debug output for immediate visibility during development
            System.Diagnostics.Debug.WriteLine(logEntry);

            string currentLogFilePath = Path.Combine(LogDirectory, LogFileName);

            try
            {
                // Check file size before writing to potentially trigger rotation
                if (File.Exists(currentLogFilePath))
                {
                    FileInfo fileInfo = new FileInfo(currentLogFilePath);
                    if (fileInfo.Length >= MaxFileSize)
                    {
                        RotateLogFiles();
                    }
                }
                File.AppendAllText(currentLogFilePath, logEntry + Environment.NewLine);
            }
            catch (Exception ex)
            {
                // If logging to file fails, ensure it's still visible in debug output
                System.Diagnostics.Debug.WriteLine($"CRITICAL ERROR: Failed to write to log file {currentLogFilePath}. Exception: {ex.Message}");
            }
        }

        // Manages log file rotation.
        private static void RotateLogFiles()
        {
            string currentLogFilePath = Path.Combine(LogDirectory, LogFileName);

            // Get all existing numbered log files (e.g., app.log.1, app.log.2)
            var existingLogFiles = Directory.GetFiles(LogDirectory, $"{Path.GetFileNameWithoutExtension(LogFileName)}.*")
                                            .Select(f => new { Path = f, Number = ParseLogFileNumber(f) })
                                            .Where(x => x.Number.HasValue)
                                            .OrderByDescending(x => x.Number) // Start from highest number (oldest)
                                            .ToList();

            // Shift existing files to higher numbers
            foreach (var file in existingLogFiles)
            {
                string newPath = Path.Combine(LogDirectory, $"{Path.GetFileNameWithoutExtension(LogFileName)}.{file.Number.Value + 1}");
                try
                {
                    if (File.Exists(newPath))
                    {
                        File.Delete(newPath); // Delete if the target file already exists
                    }
                    File.Move(file.Path, newPath);
                    System.Diagnostics.Debug.WriteLine($"Moved log file: {file.Path} to {newPath}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"ERROR: Failed to move log file {file.Path} to {newPath} during rotation. Exception: {ex.Message}");
                }
            }

            // Rename the current log file to .log.1
            if (File.Exists(currentLogFilePath))
            {
                string newCurrentLogPath = Path.Combine(LogDirectory, $"{Path.GetFileNameWithoutExtension(LogFileName)}.1");
                try
                {
                    if (File.Exists(newCurrentLogPath))
                    {
                        File.Delete(newCurrentLogPath); // Ensure target is clear
                    }
                    File.Move(currentLogFilePath, newCurrentLogPath);
                    System.Diagnostics.Debug.WriteLine($"Rotated current log file: {currentLogFilePath} to {newCurrentLogPath}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"ERROR: Failed to rotate current log file {currentLogFilePath} to {newCurrentLogPath}. Exception: {ex.Message}");
                }
            }
        }

        // Parses the numeric suffix from a log file name (e.g., "app.log.1" -> 1).
        private static int? ParseLogFileNumber(string filePath)
        {
            string fileName = Path.GetFileName(filePath);
            string extension = Path.GetExtension(fileName); // e.g., ".log" or ".1"
            string nameWithoutExtension = Path.GetFileNameWithoutExtension(fileName); // e.g., "app" or "app.log"

            // If the extension is numeric, it's a rotated file
            if (extension.Length > 1 && int.TryParse(extension.Substring(1), out int number))
            {
                // Check if the part before the number is the base log file name
                if (nameWithoutExtension.Equals(Path.GetFileNameWithoutExtension(LogFileName), StringComparison.OrdinalIgnoreCase) ||
                    nameWithoutExtension.Equals(LogFileName, StringComparison.OrdinalIgnoreCase)) // For app.log.1 case where nameWithoutExtension is "app.log"
                {
                    return number;
                }
            }
            return null;
        }
    }
}