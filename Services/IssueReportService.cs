using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using EmbyReporter.Api;

namespace EmbyReporter.Services
{
    public class IssueReportService : IService, IRequiresRequest
    {
        public IRequest Request { get; set; } = default!;
        private readonly ISessionManager _sessionManager;
        private readonly ILogger? _logger;
        private readonly ILibraryManager _libraryManager;

        public IssueReportService(ISessionManager sessionManager, ILogManager logManager, ILibraryManager libraryManager)
        {
            _sessionManager = sessionManager;
            _logger = logManager.GetLogger(GetType().Name);
            _libraryManager = libraryManager;
        }

        public object Get(GetIssueReportsRequest request)
        {
            return LogManager.GetLogEntries();
        }

        public void Post(ClearIssueReportsRequest request)
        {
            LogManager.ClearLogs();
        }

        public void Post(ReportIssueRequest request)
        {
            if (string.IsNullOrEmpty(request.ItemId))
            {
                return;
            }

            // Guard against excessively long inputs
            if (request.ItemId.Length > 256)
            {
                _logger?.Warn("[IssueReportService] ItemId exceeds maximum allowed length.");
                return;
            }

            if (!string.IsNullOrEmpty(request.Description) && request.Description.Length > 500)
            {
                request = new ReportIssueRequest
                {
                    ItemId = request.ItemId,
                    ItemName = request.ItemName,
                    Description = request.Description.Substring(0, 500)
                };
            }

            string? deviceId = null;

            try
            {
                var authHeader = Request.Headers.Get("X-Emby-Authorization");
                if (!string.IsNullOrWhiteSpace(authHeader))
                {
                    var authDict = authHeader.Split(',')
                        .Select(part => part.Trim().Split(new[] { '=' }, 2))
                        .Where(split => split.Length == 2)
                        .ToDictionary(split => split[0], split => split[1].Trim('"'));

                    if (authDict.TryGetValue("DeviceId", out var d))
                    {
                        deviceId = d;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.Warn($"Failed to parse X-Emby-Authorization header: {ex.Message}");
            }

            if (string.IsNullOrWhiteSpace(deviceId))
            {
                try
                {
                    deviceId = Request.QueryString["X-Emby-Device-Id"] ?? Request.QueryString["X-Emby-DeviceId"];
                }
                catch (Exception ex)
                {
                    _logger?.Warn($"[IssueReportService] Failed to read DeviceId from query string: {ex.Message}");
                }
            }

            if (!string.IsNullOrWhiteSpace(deviceId) && deviceId.Length > 256)
            {
                _logger?.Warn("[IssueReportService] DeviceId exceeds maximum allowed length — ignoring.");
                deviceId = null;
            }

            if (string.IsNullOrWhiteSpace(deviceId))
            {
                _logger?.Warn("DeviceId not found in headers or query string - cannot determine session for ReportIssue.");
                return;
            }

            var session = _sessionManager.Sessions.FirstOrDefault(s =>
                string.Equals(s.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase) &&
                s.IsActive);

            if (session == null)
            {
                _logger?.Warn($"Could not find active session for DeviceId: {deviceId}. Report issue failed.");
                return;
            }

            var userId = session.UserId ?? string.Empty;
            var username = session.UserName ?? "Unknown";
            var clientName = session.Client ?? "Unknown";

            string resolvedName = string.Empty;
            string libraryName = string.Empty;
            BaseItem? resolvedItem = null;

            try
            {
                if (long.TryParse(request.ItemId, out var internalId))
                {
                    resolvedItem = _libraryManager.GetItemById(internalId);
                }
            }
            catch (Exception ex)
            {
                _logger?.Warn($"Exception while querying for internal ItemId '{request.ItemId}': {ex.Message}");
            }

            if (resolvedItem != null)
            {
                if (resolvedItem is Episode episode)
                {
                    var seriesName = episode.SeriesName;
                    var seasonNumber = episode.ParentIndexNumber;
                    var episodeNumber = episode.IndexNumber;

                    if (!string.IsNullOrWhiteSpace(seriesName) && seasonNumber.HasValue && episodeNumber.HasValue)
                    {
                        resolvedName = $"{seriesName} S{seasonNumber.Value:D2}E{episodeNumber.Value:D2}";
                    }
                    else
                    {
                        resolvedName = resolvedItem.Name ?? string.Empty;
                    }
                }
                else
                {
                    resolvedName = resolvedItem.Name ?? string.Empty;
                }
            }
            else if (!string.IsNullOrWhiteSpace(request.ItemName))
            {
                resolvedName = request.ItemName;
            }
            else
            {
                var nowPlaying = session.NowPlayingItem;
                if (nowPlaying != null && !string.IsNullOrWhiteSpace(nowPlaying.Name))
                {
                    var nowPlayingName = nowPlaying.Name!;
                    _logger?.Info($"Could not resolve reported item ID '{request.ItemId}'. Falling back to NowPlayingItem '{nowPlayingName}'.");
                    resolvedName = nowPlayingName;
                }
                else
                {
                    resolvedName = request.ItemId;
                }
            }

            var itemPath = resolvedItem?.Path ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(itemPath))
            {
                try
                {
                    var libraries = _libraryManager.GetVirtualFolders();
                    foreach (var lib in libraries)
                    {
                        if (lib.Locations != null)
                        {
                            foreach (var loc in lib.Locations)
                            {
                                if (!string.IsNullOrWhiteSpace(loc) && itemPath.StartsWith(loc, StringComparison.OrdinalIgnoreCase))
                                {
                                    libraryName = lib.Name ?? string.Empty;
                                    break;
                                }
                            }
                        }
                        if (!string.IsNullOrWhiteSpace(libraryName)) break;
                    }
                }
                catch (Exception ex)
                {
                    _logger?.Warn($"[IssueReportService] Failed to determine library name for path '{itemPath}': {ex.Message}");
                }
            }

            var config = Plugin.Instance?.Configuration;
            if (config != null && config.EnablePlaybackIssueNotifications)
            {
                NotificationService.SendPlaybackIssueNotification(username, resolvedName, libraryName, request.Description);
            }

            LogManager.LogPlaybackIssue(request.ItemId, resolvedName, request.Description, libraryName, userId, username, clientName, itemPath);
        }
    }
}