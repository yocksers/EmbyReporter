using EmbyReporter.Api;
using MediaBrowser.Model.Services;

namespace EmbyReporter.Services
{
    public class PublicApiService : IService
    {
        public object Get(PublicGetIssueReportsRequest request)
        {
            return LogManager.GetLogEntries();
        }
    }
}