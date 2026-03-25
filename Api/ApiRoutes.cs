namespace EmbyReporter.Api
{
    internal static class ApiRoutes
    {
        public const string GetIssueReports = "/EmbyReporter/GetIssueReports";
        public const string ClearIssueReports = "/EmbyReporter/ClearIssueReports";
        public const string ReportIssue = "/EmbyReporter/ReportIssue";

        public const string PublicGetIssueReports = "/EmbyReporter/Public/Issues";

        public const string InjectScript = "/EmbyReporter/InjectScript";
        public const string RemoveScript = "/EmbyReporter/RemoveScript";
    }
}