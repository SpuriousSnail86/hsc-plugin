using Dalamud.Interface.Internal.Notifications;
using Dalamud.Logging;
using FileWatcherEx;
using HSC.Config;
using HSC.Models.Settings;
using System;
using System.IO;

namespace HSC.Config
{
/// <summary>
/// author: SpuriousSnail86
/// </summary>
    internal class ConfigWatcher
    {

        static FileWatcherEx.FileWatcherEx fileWatcher;

        internal static void Create()
        {
            if (fileWatcher != null)
                return;

            fileWatcher = new FileWatcherEx.FileWatcherEx(DalamudApi.api.PluginInterface.GetPluginConfigDirectory());

            fileWatcher.NotifyFilter = NotifyFilters.LastWrite;

            fileWatcher.OnChanged -= ConfigFileChanged;
            fileWatcher.OnCreated -= ConfigFileCreated;
            fileWatcher.OnChanged += ConfigFileChanged;
            fileWatcher.OnCreated += ConfigFileCreated;
            fileWatcher.Start();
        }

        internal static void Dispose()
        {
            fileWatcher.OnChanged -= ConfigFileChanged;
            fileWatcher.OnCreated -= ConfigFileCreated;
            fileWatcher?.Stop();
            fileWatcher?.Dispose();
            fileWatcher = null;
        }

        private static void HandleFileChangedOrCreated(string path)
        {

            if (Settings.SavedConfig)
            {
                Settings.SavedConfig = false;
                return;
            }

            PluginLog.Information($"File '{path}' changed.");

            if (Path.GetFileName(path).Equals(CharConfigHelpers.CharConfigFileName))
            {
                ImGuiUtil.AddNotification(NotificationType.Info, $"Reloading HSC character config file.");
                CharConfigHelpers.UpdateCharIndex(Settings.CharName);
                PluginLog.Information($"Character index for '{Settings.CharName}': {Settings.CharIndex}.");
            }
            else if (Path.GetFileName(path).Equals(Settings.HSCSettingsFileName))
            {
                ImGuiUtil.AddNotification(NotificationType.Info, $"Reloading HSC settings file.");
                Settings.Load();
                HSC.PopulateConfigFromSettings();
                PluginLog.Information($"HSC settings file loaded.");
            }
        }
        internal static void ConfigFileCreated(object sender, FileChangedEvent args)
        {
            try
            {
                if (args.ChangeType == ChangeType.CREATED)
                    HandleFileChangedOrCreated(args.FullPath);
            }
            catch (Exception ex)
            {
                PluginLog.Error($"An error occured. Message: {ex.Message}");
            }
        }

        internal static void ConfigFileChanged(object sender, FileChangedEvent args)
        {
            try
            {
                if (args.ChangeType == ChangeType.CHANGED)
                    HandleFileChangedOrCreated(args.FullPath);
            }
            catch (Exception ex)
            {
                PluginLog.Error($"An error occured. Message: {ex.Message}");
            }
        }
    }
}
