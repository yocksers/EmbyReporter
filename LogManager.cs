using MediaBrowser.Common.Configuration;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace EmbyReporter
{
    public class LogEntry
    {
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string EventType { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string ClientName { get; set; } = string.Empty;
        public string ItemId { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public string LibraryName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
    }

    public static class LogManager
    {
        private static ILogger? _logger;
        private static IJsonSerializer? _jsonSerializer;
        private static string? _logFilePath;
        private static bool _isRunning = false;

        private static readonly object _saveLock = new object();

        private static readonly ConcurrentQueue<LogEntry> _logEntries = new();
        public const int MaxLogEntries = 200;

        public static void Start(ILogger logger, IJsonSerializer jsonSerializer, IApplicationPaths appPaths)
        {
            if (_isRunning) return;

            _logger = logger;
            _jsonSerializer = jsonSerializer;
            var basePath = appPaths.DataPath;
            if (string.IsNullOrEmpty(basePath))
            {
                basePath = appPaths.PluginConfigurationsPath;
            }

            var pluginDir = Path.Combine(basePath, "EmbyReporter");
            _logFilePath = Path.Combine(pluginDir, "PlaybackReporter.Logging.json");

            LoadLogs();

            _isRunning = true;
            _logger.Info($"[LogManager] Started. Log file: {_logFilePath}");
        }

        public static void Stop()
        {
            SaveLogs();
            _isRunning = false;
            _logger?.Info("[LogManager] Stopped.");
        }

        public static void LogPlaybackIssue(string itemId, string itemName, string description, string libraryName, string userId, string username, string clientName, string path)
        {
            var namePart = string.IsNullOrWhiteSpace(itemName) ? itemId : itemName;
            var message = $"Playback issue reported for '{namePart}'.";
            if (!string.IsNullOrWhiteSpace(description))
            {
                message += $" Description: {description}";
            }

            if (!string.IsNullOrWhiteSpace(libraryName))
            {
                message += $" Library: {libraryName}";
            }

            AddLogEntry("Playback Issue", userId, username, clientName, message, itemId, itemName, description, libraryName, path);
        }

        private static void AddLogEntry(string eventType, string userId, string username, string clientName, string message, string itemId = "", string itemName = "", string description = "", string libraryName = "", string path = "")
        {
            if (!_isRunning) return;

            var entry = new LogEntry
            {
                EventType = eventType,
                UserId = userId ?? string.Empty,
                Username = username ?? string.Empty,
                ClientName = clientName ?? string.Empty,
                ItemId = itemId ?? string.Empty,
                ItemName = itemName ?? string.Empty,
                LibraryName = libraryName ?? string.Empty,
                Description = description ?? string.Empty,
                Message = message,
                Path = path ?? string.Empty
            };
            _logEntries.Enqueue(entry);

            while (_logEntries.Count > MaxLogEntries)
            {
                _logEntries.TryDequeue(out _);
            }

            SaveLogs();
        }

        public static IReadOnlyList<LogEntry> GetLogEntries()
        {
            return _logEntries.OrderByDescending(e => e.Timestamp).ToList();
        }

        public static void ClearLogs()
        {
            _logEntries.Clear();
            SaveLogs();
            _logger?.Info("[LogManager] All log entries have been cleared.");
        }

        private static void LoadLogs()
        {
            if (_jsonSerializer == null || string.IsNullOrEmpty(_logFilePath) || !File.Exists(_logFilePath)) return;

            try
            {
                var json = File.ReadAllText(_logFilePath);
                var logs = _jsonSerializer.DeserializeFromString<List<LogEntry>>(json);
                if (logs != null)
                {
                    foreach (var log in logs)
                    {
                        _logEntries.Enqueue(log);
                    }
                }
                _logger?.Info($"[LogManager] Loaded {_logEntries.Count} log entries from file.");
            }
            catch (Exception ex)
            {
                _logger?.ErrorException("[LogManager] Error loading logging data.", ex);
            }
        }

        private static void SaveLogs()
        {
            if (_jsonSerializer == null || string.IsNullOrEmpty(_logFilePath)) return;

            lock (_saveLock)
            {
                try
                {
                    var json = _jsonSerializer.SerializeToString(_logEntries.ToList());
                    var tempFilePath = _logFilePath + ".tmp";

                    var dir = Path.GetDirectoryName(_logFilePath);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }

                    File.WriteAllText(tempFilePath, json);

                    if (File.Exists(_logFilePath))
                    {
                        File.Replace(tempFilePath, _logFilePath, null);
                    }
                    else
                    {
                        File.Move(tempFilePath, _logFilePath);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.ErrorException("[LogManager] Error saving logging data.", ex);
                }
            }
        }
    }
}