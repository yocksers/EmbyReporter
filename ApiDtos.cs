using MediaBrowser.Model.Services;
using System.Collections.Generic;

namespace EmbyReporter.Api
{
    [Route(ApiRoutes.GetIssueReports, "GET", Summary = "Gets recent issue reports from the plugin.")]
    public class GetIssueReportsRequest : IReturn<List<LogEntry>> { }

    [Route(ApiRoutes.ClearIssueReports, "POST", Summary = "Clears all issue reports from the plugin.")]
    public class ClearIssueReportsRequest : IReturnVoid { }

    [Route(ApiRoutes.ReportIssue, "POST", Summary = "Reports a playback issue for a media item.")]
    public class ReportIssueRequest : IReturnVoid
    {
        public string ItemId { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    [Route(ApiRoutes.PublicGetIssueReports, "GET", Summary = "Gets the list of all reported playback issues.")]
    public class PublicGetIssueReportsRequest : IReturn<List<LogEntry>>
    {
        public int? Skip { get; set; }
        public int? Take { get; set; }
    }

    [Route(ApiRoutes.InjectScript, "POST", Summary = "Backs up and replaces actionsheet.js, deploys emby-reporter-badge.js, and injects its script tag into index.html.")]
    public class InjectScriptRequest : IReturn<ScriptInjectionResult> { }

    [Route(ApiRoutes.RemoveScript, "POST", Summary = "Restores original actionsheet.js, deletes emby-reporter-badge.js, and removes its script tag from index.html.")]
    public class RemoveScriptRequest : IReturn<ScriptInjectionResult> { }

    public class ScriptInjectionResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    [Route(ApiRoutes.AdminReply, "POST", Summary = "Posts an admin reply to a reported issue.")]
    public class AdminReplyRequest : IReturnVoid
    {
        public string ReportId { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    [Route(ApiRoutes.UserResponse, "POST", Summary = "Posts a user confirmation or denial that a reported issue was fixed.")]
    public class UserResponseRequest : IReturnVoid
    {
        public string ReportId { get; set; } = string.Empty;
        public bool Confirmed { get; set; }
        public string Text { get; set; } = string.Empty;
    }

    [Route(ApiRoutes.GetMyMessages, "GET", Summary = "Returns reports that have an unread admin reply for the current user.")]
    public class GetMyMessagesRequest : IReturn<List<PendingReportDto>> { }

    [Route(ApiRoutes.GetMyReports, "GET", Summary = "Returns all active reports for the current user.")]
    public class GetMyReportsRequest : IReturn<List<PendingReportDto>> { }

    [Route(ApiRoutes.UserComment, "POST", Summary = "Posts a free-form user comment on a report without changing its status.")]
    public class UserCommentRequest : IReturnVoid
    {
        public string ReportId { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
    }

    [Route(ApiRoutes.DeleteReport, "POST", Summary = "Deletes a specific issue report.")]
    public class DeleteReportRequest : IReturnVoid
    {
        public string ReportId { get; set; } = string.Empty;
    }

    [Route(ApiRoutes.SetReportStatus, "POST", Summary = "Sets the admin status on a reported issue.")]
    public class SetReportStatusRequest : IReturnVoid
    {
        public string ReportId { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }

    public class PendingReportDto
    {
        public string ReportId { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public List<ChatMessageDto> Messages { get; set; } = new List<ChatMessageDto>();
    }

    public class ChatMessageDto
    {
        public string Sender { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public string Timestamp { get; set; } = string.Empty;
    }
}