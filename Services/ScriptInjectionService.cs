using EmbyReporter.Api;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Services;
using System;
using System.IO;
using System.Reflection;

namespace EmbyReporter.Services
{
    public class ScriptInjectionService : IService
    {
        private const string ActionSheetResource = "EmbyReporter.Script.actionsheet.js";
        private const string ActionSheetRelative = "dashboard-ui/modules/actionsheet/actionsheet.js";

        private const string BadgeResource = "EmbyReporter.Script.notifications-badge.js";
        private const string BadgeRelative = "dashboard-ui/emby-reporter-badge.js";
        private const string IndexHtmlRelative = "dashboard-ui/index.html";
        private const string BadgeScriptTag = "<script src=\"emby-reporter-badge.js\"></script>";

        private readonly ILogger _logger;

        public ScriptInjectionService(ILogManager logManager)
        {
            _logger = logManager.GetLogger(GetType().Name);
        }

        private static string ActionSheetPath => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, ActionSheetRelative));
        private static string ActionSheetBackup => ActionSheetPath + ".bak";
        private static string BadgePath => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, BadgeRelative));
        private static string IndexHtmlPath => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, IndexHtmlRelative));
        private static string IndexHtmlBackup => IndexHtmlPath + ".bak";

        private static bool IsPathWithinBaseDirectory(string fullPath)
        {
            var baseDir = Path.GetFullPath(AppContext.BaseDirectory)
                             .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                          + Path.DirectorySeparatorChar;
            return fullPath.StartsWith(baseDir, StringComparison.OrdinalIgnoreCase);
        }

        public object Post(InjectScriptRequest request)
        {
            var actionSheetPath = ActionSheetPath;
            var actionSheetBackup = ActionSheetBackup;
            var badgePath = BadgePath;
            var indexPath = IndexHtmlPath;
            var indexBackup = IndexHtmlBackup;

            if (!IsPathWithinBaseDirectory(actionSheetPath) ||
                !IsPathWithinBaseDirectory(badgePath) ||
                !IsPathWithinBaseDirectory(indexPath))
            {
                _logger.Error("[ScriptInjectionService] A resolved path is outside the Emby base directory.");
                return new ScriptInjectionResult { Success = false, Message = "Security error: a target path is outside the allowed directory." };
            }

            if (!File.Exists(actionSheetPath))
            {
                _logger.Warn($"[ScriptInjectionService] actionsheet.js not found at: {actionSheetPath}");
                return new ScriptInjectionResult { Success = false, Message = $"actionsheet.js not found at: {actionSheetPath}" };
            }

            if (!File.Exists(indexPath))
            {
                _logger.Warn($"[ScriptInjectionService] index.html not found at: {indexPath}");
                return new ScriptInjectionResult { Success = false, Message = $"index.html not found at: {indexPath}" };
            }

            if (File.Exists(actionSheetBackup))
            {
                return new ScriptInjectionResult { Success = false, Message = "Script is already installed." };
            }

            var assembly = typeof(Plugin).GetTypeInfo().Assembly;

            // Replace actionsheet.js
            using (var stream = assembly.GetManifestResourceStream(ActionSheetResource))
            {
                if (stream == null)
                {
                    _logger.Error($"[ScriptInjectionService] Embedded resource '{ActionSheetResource}' not found.");
                    return new ScriptInjectionResult { Success = false, Message = "Internal error: embedded actionsheet.js resource not found." };
                }

                File.Copy(actionSheetPath, actionSheetBackup, overwrite: true);
                using var reader = new StreamReader(stream);
                File.WriteAllText(actionSheetPath, reader.ReadToEnd());
            }

            // Deploy badge script
            using (var stream = assembly.GetManifestResourceStream(BadgeResource))
            {
                if (stream == null)
                {
                    _logger.Error($"[ScriptInjectionService] Embedded resource '{BadgeResource}' not found.");
                    File.Copy(actionSheetBackup, actionSheetPath, overwrite: true);
                    File.Delete(actionSheetBackup);
                    return new ScriptInjectionResult { Success = false, Message = "Internal error: embedded notifications-badge.js resource not found." };
                }

                using var reader = new StreamReader(stream);
                File.WriteAllText(badgePath, reader.ReadToEnd());
            }

            // Inject script tag into index.html
            var indexContent = File.ReadAllText(indexPath);
            if (!indexContent.Contains(BadgeScriptTag))
            {
                File.Copy(indexPath, indexBackup, overwrite: true);
                indexContent = indexContent.Replace("</body>", BadgeScriptTag + "\n</body>");
                File.WriteAllText(indexPath, indexContent);
            }

            _logger.Info("[ScriptInjectionService] Client scripts installed.");
            return new ScriptInjectionResult { Success = true, Message = "Client scripts installed successfully. Refresh the Emby web client to activate them." };
        }

        public object Post(RemoveScriptRequest request)
        {
            var actionSheetPath = ActionSheetPath;
            var actionSheetBackup = ActionSheetBackup;
            var badgePath = BadgePath;
            var indexPath = IndexHtmlPath;
            var indexBackup = IndexHtmlBackup;

            if (!IsPathWithinBaseDirectory(actionSheetPath) ||
                !IsPathWithinBaseDirectory(badgePath) ||
                !IsPathWithinBaseDirectory(indexPath))
            {
                _logger.Error("[ScriptInjectionService] A resolved path is outside the Emby base directory.");
                return new ScriptInjectionResult { Success = false, Message = "Security error: a target path is outside the allowed directory." };
            }

            if (!File.Exists(actionSheetBackup))
            {
                return new ScriptInjectionResult { Success = false, Message = "Script is not currently installed (no backup found)." };
            }

            File.Copy(actionSheetBackup, actionSheetPath, overwrite: true);
            File.Delete(actionSheetBackup);

            if (File.Exists(badgePath))
                File.Delete(badgePath);

            if (File.Exists(indexPath))
            {
                if (File.Exists(indexBackup))
                {
                    File.Copy(indexBackup, indexPath, overwrite: true);
                    File.Delete(indexBackup);
                }
                else
                {
                    // No backup — strip the injected script tag directly so no dangling reference remains
                    var indexContent = File.ReadAllText(indexPath);
                    if (indexContent.Contains(BadgeScriptTag))
                    {
                        indexContent = indexContent
                            .Replace(BadgeScriptTag + "\n</body>", "</body>")
                            .Replace(BadgeScriptTag + "\r\n</body>", "</body>")
                            .Replace(BadgeScriptTag, string.Empty);
                        File.WriteAllText(indexPath, indexContent);
                    }
                }
            }

            _logger.Info("[ScriptInjectionService] Client scripts removed. Originals restored.");
            return new ScriptInjectionResult { Success = true, Message = "Client scripts removed successfully. Originals restored. Refresh the Emby web client to deactivate them." };
        }
    }
}
