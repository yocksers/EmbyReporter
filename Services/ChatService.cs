using EmbyReporter.Api;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace EmbyReporter.Services
{
    public class ChatService : IService, IRequiresRequest
    {
        public IRequest Request { get; set; } = default!;
        private readonly ISessionManager _sessionManager;
        private readonly ILogger? _logger;

        public ChatService(ISessionManager sessionManager, ILogManager logManager)
        {
            _sessionManager = sessionManager;
            _logger = logManager.GetLogger(GetType().Name);
        }

        private string? GetCurrentUserId()
        {
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
                        deviceId = d;
                }
            }
            catch (Exception ex)
            {
                _logger?.Warn($"[ChatService] Failed to parse auth header: {ex.Message}");
            }

            if (string.IsNullOrWhiteSpace(deviceId))
            {
                try
                {
                    deviceId = Request.QueryString["X-Emby-Device-Id"] ?? Request.QueryString["X-Emby-DeviceId"];
                }
                catch (Exception ex)
                {
                    _logger?.Warn($"[ChatService] Failed to read DeviceId from query: {ex.Message}");
                }
            }

            if (string.IsNullOrWhiteSpace(deviceId)) return null;

            var session = _sessionManager.Sessions.FirstOrDefault(s =>
                string.Equals(s.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase) && s.IsActive);

            return session?.UserId ?? null;
        }

        public object Get(GetMyMessagesRequest request)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(userId))
                return new List<PendingReportDto>();

            var pending = LogManager.GetPendingReportsForUser(userId);

            var result = pending.Select(entry => new PendingReportDto
            {
                ReportId = entry.ReportId,
                ItemName = entry.ItemName,
                Description = entry.Description,
                Status = entry.Status,
                Messages = (entry.Messages ?? new System.Collections.Generic.List<ChatMessage>())
                    .Select(m => new ChatMessageDto
                    {
                        Sender = m.Sender,
                        Text = m.Text,
                        Timestamp = m.Timestamp.ToString("o")
                    }).ToList()
            }).ToList();

            return result;
        }

        public void Post(UserCommentRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.ReportId) || request.ReportId.Length > 64)
            {
                _logger?.Warn("[ChatService] UserComment: invalid ReportId.");
                return;
            }

            var userId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(userId))
            {
                _logger?.Warn("[ChatService] UserComment: could not identify user.");
                return;
            }

            var active = LogManager.GetActiveReportsForUser(userId);
            var report = active.FirstOrDefault(r =>
                string.Equals(r.ReportId, request.ReportId, StringComparison.Ordinal));

            if (report == null)
            {
                _logger?.Warn($"[ChatService] UserComment: report {request.ReportId} not found or does not belong to this user.");
                return;
            }

            var text = string.IsNullOrWhiteSpace(request.Text)
                ? string.Empty
                : request.Text.Length > 500 ? request.Text.Substring(0, 500) : request.Text;

            LogManager.AddUserComment(request.ReportId, text);
        }

        public void Post(UserResponseRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.ReportId) || request.ReportId.Length > 64)
            {
                _logger?.Warn("[ChatService] UserResponse: invalid ReportId.");
                return;
            }

            var userId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(userId))
            {
                _logger?.Warn("[ChatService] UserResponse: could not identify user.");
                return;
            }

            var pending = LogManager.GetActiveReportsForUser(userId);
            var report = pending.FirstOrDefault(r =>
                string.Equals(r.ReportId, request.ReportId, StringComparison.Ordinal));

            if (report == null)
            {
                _logger?.Warn($"[ChatService] UserResponse: report {request.ReportId} not found or does not belong to this user.");
                return;
            }

            var text = string.IsNullOrWhiteSpace(request.Text)
                ? string.Empty
                : request.Text.Length > 500 ? request.Text.Substring(0, 500) : request.Text;

            LogManager.AddUserMessage(request.ReportId, request.Confirmed, text);
        }

        public object Get(GetMyReportsRequest request)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(userId))
                return new List<PendingReportDto>();

            var active = LogManager.GetActiveReportsForUser(userId);

            return active.Select(entry => new PendingReportDto
            {
                ReportId = entry.ReportId,
                ItemName = entry.ItemName,
                Description = entry.Description,
                Status = entry.Status,
                Messages = (entry.Messages ?? new System.Collections.Generic.List<ChatMessage>())
                    .Select(m => new ChatMessageDto
                    {
                        Sender = m.Sender,
                        Text = m.Text,
                        Timestamp = m.Timestamp.ToString("o")
                    }).ToList()
            }).ToList();
        }
    }
}
