using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace ImAdjustr.Internal.Services{
    public class DebugLogger : ILogger {
        private readonly string _name;
        private readonly string _logFilePath;
        public DebugLogger(Process currentProcess, string filePath) {
            _name = currentProcess.ProcessName;
            _logFilePath = filePath;
            Directory.CreateDirectory(Path.GetDirectoryName(_logFilePath));
            if (File.Exists(_logFilePath)) File.WriteAllText(_logFilePath, string.Empty);
            File.AppendAllText(_logFilePath, $"{currentProcess.StartTime:yyyy-MM-dd HH:mm:ss} - Running {_name}({currentProcess.Id}) " +
                $"from {Assembly.GetExecutingAssembly().Location}, with {RuntimeInformation.FrameworkDescription}, " +
                $"CLR {Assembly.GetExecutingAssembly().ImageRuntimeVersion} ({RuntimeEnvironment.GetRuntimeDirectory()})" + Environment.NewLine);
        }

        public IDisposable BeginScope<TState>(TState state) => null;
        public bool IsEnabled(LogLevel logLevel) => true; // Customize based on log level

        private string LogLevelFormatted(LogLevel level) {
            if (level == LogLevel.Information) return "INFO";
            else if (level == LogLevel.Error) return "ERROR";
            else if (level == LogLevel.Warning) return "WARNING";
            else if (level == LogLevel.Debug) return "DEBUG";
            else return " - ";
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter) {
            if (formatter != null && IsEnabled(logLevel)) {
                var message = formatter(state, exception);
                var logMessage = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{LogLevelFormatted(logLevel)}] {_name} : {message} {(exception is null ? "" : ('('+ exception.Message +')'))}";
                File.AppendAllText(_logFilePath, logMessage + Environment.NewLine);
            }
        }

        public void Info(string message) => Log(LogLevel.Information, new EventId(), message, null, (msg, _) => msg);
        public void Warning(string message) => Log(LogLevel.Warning, new EventId(), message, null, (msg, _) => msg);
        public void Error(string message, Exception ex = null) => Log(LogLevel.Error, new EventId(), message, ex, (msg, _) => msg);
        public void Debug(string message) => Log(LogLevel.Debug, new EventId(), message, null, (msg, _) => msg);
    }
}
