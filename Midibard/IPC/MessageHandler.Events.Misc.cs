using Dalamud.Interface.Internal.Notifications;
using Dalamud.Logging;
using HSC.Helpers;
using HSC.Managers.Agents;
using HSC.Managers.Ipc;
using HSC.Midi;
using HSC.Models.Settings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HSC.IPC
{
    /// <summary>
    /// author:  SpuriousSnail86
    /// </summary>
    internal partial class MessageHandler
    {
        public static void OnChangeSongMessageReceived(object sender, int index)
        {
            if (!IpcManager.Connected)
                return;

            PluginLog.Information($"Received change song '{index}' message.");
            Task.Run(() => PlaylistManager.ChangeSong(index));
        }

        public static void OnReloadPlaylistSettingsMessageReceived(object sender, EventArgs e)
        {
            if (!IpcManager.Connected)
                return;

            PluginLog.Information($"Received reload playlist settings message.");
            Task.Run(() => PlaylistManager.ReloadSettingsAndSwitch());
        }

        public static void OnReloadPlaylistMessageReceived(object sender, EventArgs e)
        {
            //if (DalamudApi.api.PartyList.IsInParty() && !Settings.IsLeader)
            //    return;
            if (!IpcManager.Connected)
                return;

            PluginLog.Information($"Received reload playlist message.");
            Task.Run(() => PlaylistManager.Reload());
        }

        public static void OnSwitchInstrumentsMessageReceived(object sender, EventArgs e)
        {
            if (!IpcManager.Connected)
                return;

            PluginLog.Information($"Received switch instruments message.");
            Task.Run(() => PlaylistManager.SwitchInstruments());
        }

        public static void OnClosePerformanceMessageReceived(object sender, EventArgs e)
        {
            if (!IpcManager.Connected)
                return;

            PluginLog.Information($"Received close performance message.");
            Task.Run(() => PerformanceHelpers.ClosePerformance());
        }

        public static void OnConnectMessageReceived(object sender, int index)
        {
            PluginLog.Information($"Received connect message.");
            if (index == Settings.CharIndex)
                IpcManager.Connect();
        }


        public static void OnDisconnectMessageReceived(object sender, int index)
        {
            PluginLog.Information($"Received disconnect message.");
            if (index == Settings.CharIndex)
                IpcManager.Disconnect();
        }

        //public static void OnOpenInstrumentMessageReceived(object sender,  Dictionary<int, Instrument> args)
        //{
        //    try
        //    {
        //        if (DalamudApi.api.PartyList.IsInParty() && Settings.IsLeader)
        //            return;

        //        PluginLog.Information($"Received open instrument message.");

        //        if (Settings.CharIndex == -1)
        //        {
        //            ImGuiUtil.AddNotification(NotificationType.Error, $"Cannot open instrument. Character config not loaded for '{Settings.CharName}'.");
        //            return;
        //        }

        //        var ins = args[Settings.CharIndex];

        //        PerformanceHelpers.SwitchTo((uint)ins);
        //    }
        //    catch (Exception ex)
        //    {

        //    }

        //}
    }
}
