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

        public const string AdminReply = "/EmbyReporter/Reports/Reply";
        public const string UserResponse = "/EmbyReporter/Reports/UserResponse";
        public const string GetMyMessages = "/EmbyReporter/MyMessages";

        public const string DeleteReport = "/EmbyReporter/Reports/Delete";
        public const string SetReportStatus = "/EmbyReporter/Reports/SetStatus";
        public const string GetMyReports = "/EmbyReporter/MyReports";
        public const string UserComment = "/EmbyReporter/Reports/UserComment";
    }
}