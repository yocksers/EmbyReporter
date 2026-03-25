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
    public class PublicGetIssueReportsRequest : IReturn<List<LogEntry>> { }

    [Route(ApiRoutes.InjectScript, "POST", Summary = "Copies report.js into dashboard-ui and injects its script tag into index.html.")]
    public class InjectScriptRequest : IReturn<ScriptInjectionResult> { }

    [Route(ApiRoutes.RemoveScript, "POST", Summary = "Removes the report.js script tag from index.html and deletes the file.")]
    public class RemoveScriptRequest : IReturn<ScriptInjectionResult> { }

    public class ScriptInjectionResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}