using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller.Notifications;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.Drawing;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using EmbyReporter.Api;

namespace EmbyReporter
{
    public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages, IHasThumbImage, IServerEntryPoint
    {
        private readonly ILogger _logger;
        private readonly IJsonSerializer _jsonSerializer;
        private readonly IApplicationPaths _appPaths;
        private readonly INotificationManager _notificationManager;
        public static Plugin? Instance { get; private set; }

        public override string Name => "Emby Reporter";
        public override string Description => "A plugin for users to report playback issues via a bookmarklet.";
        public override Guid Id => Guid.Parse("decab536-f5ca-4810-88c2-0e60f652b921");

        public Plugin(IApplicationPaths appPaths, IXmlSerializer xmlSerializer, ILogManager logManager, IJsonSerializer jsonSerializer, INotificationManager notificationManager)
            : base(appPaths, xmlSerializer)
        {
            Instance = this;
            _logger = logManager.GetLogger(GetType().Name);
            _jsonSerializer = jsonSerializer;
            _appPaths = appPaths;
            _notificationManager = notificationManager;
        }

        public IEnumerable<PluginPageInfo> GetPages()
        {
            return new[]
            {
                new PluginPageInfo
                {
                    Name = "PlaybackReporterConfiguration",
                    EmbeddedResourcePath = "EmbyReporter.Configuration.PlaybackReporterConfiguration.html",
                },
                new PluginPageInfo
                {
                    Name = "PlaybackReporterConfigurationjs",
                    EmbeddedResourcePath = "EmbyReporter.Configuration.PlaybackReporterConfiguration.js"
                }
            };
        }

        public void Run()
        {
            LogManager.Start(_logger, _jsonSerializer, _appPaths);
            NotificationService.Start(_logger, _notificationManager);
        }

        public void Dispose()
        {
            LogManager.Stop();
            NotificationService.Stop();
        }

        public Stream GetThumbImage()
        {
            var assembly = typeof(Plugin).GetTypeInfo().Assembly;
            var resourceName = typeof(Plugin).Namespace + ".Images.logo.jpg";
            return assembly.GetManifestResourceStream(resourceName) ?? Stream.Null;
        }

        public ImageFormat ThumbImageFormat => ImageFormat.Jpg;
    }
}