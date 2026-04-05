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
    public class ChatMessage
    {
        public string Sender { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

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
        public string ReportId { get; set; } = string.Empty;
        public string Status { get; set; } = "Open";
        public List<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
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
                Path = path ?? string.Empty,
                ReportId = Guid.NewGuid().ToString("N"),
                Status = "Open",
                Messages = new List<ChatMessage>()
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

        public static bool AddAdminMessage(string reportId, string text)
        {
            if (string.IsNullOrWhiteSpace(reportId) || string.IsNullOrWhiteSpace(text)) return false;

            var entry = _logEntries.FirstOrDefault(e => string.Equals(e.ReportId, reportId, StringComparison.Ordinal));
            if (entry == null) return false;

            if (entry.Messages == null) entry.Messages = new List<ChatMessage>();
            entry.Messages.Add(new ChatMessage { Sender = "admin", Text = text });
            entry.Status = "AwaitingUserResponse";
            SaveLogs();
            _logger?.Info($"[LogManager] Admin replied to report {reportId}.");
            return true;
        }

        public static bool AddUserComment(string reportId, string text)
        {
            if (string.IsNullOrWhiteSpace(reportId) || string.IsNullOrWhiteSpace(text)) return false;

            var entry = _logEntries.FirstOrDefault(e => string.Equals(e.ReportId, reportId, StringComparison.Ordinal));
            if (entry == null) return false;

            if (entry.Messages == null) entry.Messages = new List<ChatMessage>();
            entry.Messages.Add(new ChatMessage { Sender = "user", Text = text });
            SaveLogs();
            _logger?.Info($"[LogManager] User commented on report {reportId}.");
            return true;
        }

        public static bool AddUserMessage(string reportId, bool confirmed, string text)
        {
            if (string.IsNullOrWhiteSpace(reportId)) return false;

            var entry = _logEntries.FirstOrDefault(e => string.Equals(e.ReportId, reportId, StringComparison.Ordinal));
            if (entry == null) return false;

            if (!string.IsNullOrWhiteSpace(text))
            {
                if (entry.Messages == null) entry.Messages = new List<ChatMessage>();
                entry.Messages.Add(new ChatMessage { Sender = "user", Text = text });
            }
            entry.Status = confirmed ? "Confirmed" : "StillBroken";
            SaveLogs();
            _logger?.Info($"[LogManager] User responded to report {reportId}: {entry.Status}.");
            return true;
        }

        public static IReadOnlyList<LogEntry> GetPendingReportsForUser(string userId)
        {
            return _logEntries
                .Where(e => string.Equals(e.UserId, userId, StringComparison.OrdinalIgnoreCase)
                         && string.Equals(e.Status, "AwaitingUserResponse", StringComparison.Ordinal))
                .OrderByDescending(e => e.Timestamp)
                .ToList();
        }

        public static IReadOnlyList<LogEntry> GetActiveReportsForUser(string userId)
        {
            return _logEntries
                .Where(e => string.Equals(e.UserId, userId, StringComparison.OrdinalIgnoreCase)
                         && !string.Equals(e.Status, "Confirmed", StringComparison.Ordinal))
                .OrderByDescending(e => e.Timestamp)
                .ToList();
        }

        public static string? GetReportUserId(string reportId)
        {
            return _logEntries.FirstOrDefault(e =>
                string.Equals(e.ReportId, reportId, StringComparison.Ordinal))?.UserId;
        }

        public static bool DeleteReport(string reportId)
        {
            if (string.IsNullOrWhiteSpace(reportId)) return false;

            var all = _logEntries.ToList();
            if (!all.Any(e => string.Equals(e.ReportId, reportId, StringComparison.Ordinal))) return false;

            var remaining = all.Where(e => !string.Equals(e.ReportId, reportId, StringComparison.Ordinal)).ToList();
            _logEntries.Clear();
            foreach (var e in remaining) _logEntries.Enqueue(e);

            SaveLogs();
            _logger?.Info($"[LogManager] Deleted report {reportId}.");
            return true;
        }

        public static bool SetReportStatus(string reportId, string status)
        {
            var allowed = new[] { "Acknowledged", "WorkingOnIt", "Fixed" };
            if (!allowed.Contains(status)) return false;

            var entry = _logEntries.FirstOrDefault(e => string.Equals(e.ReportId, reportId, StringComparison.Ordinal));
            if (entry == null) return false;

            entry.Status = status;
            if (entry.Messages == null) entry.Messages = new List<ChatMessage>();

            var label = status == "Acknowledged" ? "Acknowledged"
                      : status == "WorkingOnIt"  ? "Working on it"
                      : "Marked as fixed";

            entry.Messages.Add(new ChatMessage { Sender = "system", Text = $"Status changed to: {label}" });
            SaveLogs();
            _logger?.Info($"[LogManager] Status of report {reportId} set to {status}.");
            return true;
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