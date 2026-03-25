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
        private const string EmbeddedResourceName = "EmbyReporter.Script.actionsheet.js";
        private const string RelativeTarget = "dashboard-ui/modules/actionsheet/actionsheet.js";

        private readonly ILogger _logger;

        public ScriptInjectionService(ILogManager logManager)
        {
            _logger = logManager.GetLogger(GetType().Name);
        }

        private static string TargetPath => Path.Combine(AppContext.BaseDirectory, RelativeTarget);
        private static string BackupPath => TargetPath + ".bak";

        public object Post(InjectScriptRequest request)
        {
            var targetPath = TargetPath;
            var backupPath = BackupPath;

            if (!File.Exists(targetPath))
            {
                _logger.Warn($"[ScriptInjectionService] actionsheet.js not found at: {targetPath}");
                return new ScriptInjectionResult { Success = false, Message = $"actionsheet.js not found at: {targetPath}" };
            }

            if (File.Exists(backupPath))
            {
                return new ScriptInjectionResult { Success = false, Message = "Script is already installed." };
            }

            var assembly = typeof(Plugin).GetTypeInfo().Assembly;
            using (var stream = assembly.GetManifestResourceStream(EmbeddedResourceName))
            {
                if (stream == null)
                {
                    _logger.Error($"[ScriptInjectionService] Embedded resource '{EmbeddedResourceName}' not found.");
                    return new ScriptInjectionResult { Success = false, Message = "Internal error: embedded actionsheet.js resource not found." };
                }

                File.Copy(targetPath, backupPath, overwrite: true);

                using var reader = new StreamReader(stream);
                File.WriteAllText(targetPath, reader.ReadToEnd());
            }

            _logger.Info("[ScriptInjectionService] actionsheet.js replaced. Original backed up.");
            return new ScriptInjectionResult { Success = true, Message = "Client script installed successfully. Refresh the Emby web client to activate it." };
        }

        public object Post(RemoveScriptRequest request)
        {
            var targetPath = TargetPath;
            var backupPath = BackupPath;

            if (!File.Exists(backupPath))
            {
                return new ScriptInjectionResult { Success = false, Message = "Script is not currently installed (no backup found)." };
            }

            File.Copy(backupPath, targetPath, overwrite: true);
            File.Delete(backupPath);

            _logger.Info("[ScriptInjectionService] Original actionsheet.js restored.");
            return new ScriptInjectionResult { Success = true, Message = "Client script removed successfully. Original file restored. Refresh the Emby web client to deactivate it." };
        }
    }
}
