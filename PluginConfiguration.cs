﻿using MediaBrowser.Model.Plugins;
using System;

namespace EmbyReporter
{
    public class PluginConfiguration : BasePluginConfiguration
    {
        public string ConfigurationVersion { get; set; } = Guid.NewGuid().ToString();
        public bool EnablePlaybackIssueNotifications { get; set; } = true;
    }
}