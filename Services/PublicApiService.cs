using EmbyReporter.Api;
using MediaBrowser.Model.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace EmbyReporter.Services
{
    public class PublicApiService : IService
    {
        public object Get(PublicGetIssueReportsRequest request)
        {
            IEnumerable<LogEntry> entries = LogManager.GetLogEntries();

            if (request.Skip.HasValue && request.Skip.Value > 0)
                entries = entries.Skip(request.Skip.Value);

            if (request.Take.HasValue && request.Take.Value > 0)
                entries = entries.Take(Math.Min(request.Take.Value, LogManager.MaxLogEntries));

            return entries.ToList();
        }
    }
}