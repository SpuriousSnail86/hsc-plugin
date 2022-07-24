using Dalamud.Interface.Internal.Notifications;
using Dalamud.Logging;
using HSC.Control.MidiControl;
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
        public static void OnPlayMessageReceived(object sender, EventArgs args)
        {
            if (!IpcManager.Connected)
                return;

            if (DalamudApi.api.PartyList.IsInParty() && !Settings.IsLeader)
                return;

            PluginLog.Information($"Received play message.");
            MidiPlayerControl.Play();
        }
        public static void OnPauseMessageReceived(object sender, EventArgs args)
        {
            if (!IpcManager.Connected)
                return;
            //if (DalamudApi.api.PartyList.IsInParty() && !Settings.IsLeader)
            //    return;

            PluginLog.Information($"Received pause message.");
            MidiPlayerControl.Pause();
        }

        public static void OnStopMessageReceived(object sender, EventArgs args)
        {
            if (!IpcManager.Connected)
                return;
            //if (DalamudApi.api.PartyList.IsInParty() && !Settings.IsLeader)
            //    return;

            PluginLog.Information($"Received stop message.");
            MidiPlayerControl.Stop();
        }


        public static void OnNextMessageReceived(object sender, EventArgs args)
        {
            if (!IpcManager.Connected)
                return;
            //if (DalamudApi.api.PartyList.IsInParty() && !Settings.IsLeader)
            //    return;

            PluginLog.Information($"Received next song message.");
            MidiPlayerControl.Next();
        }

        public static void OnPreviousMessageReceived(object sender, EventArgs args)
        {
            if (!IpcManager.Connected)
                return;
            //if (DalamudApi.api.PartyList.IsInParty() && !Settings.IsLeader)
            //    return;

            PluginLog.Information($"Received previous song message.");
            MidiPlayerControl.Prev();
        }

        public static void OnChangeSpeedMessageReceived(object sender, float speed)
        {
            if (!IpcManager.Connected)
                return;
            //if (DalamudApi.api.PartyList.IsInParty() && !Settings.IsLeader)
            //    return;

            PluginLog.Information($"Received change speed '{speed}' message.");
            MidiPlayerControl.SetSpeed(speed);
        }

        public static void OnSeekMessageReceived(object sender, long ms)
        {
            if (!IpcManager.Connected)
                return;
            //if (DalamudApi.api.PartyList.IsInParty() && !Settings.IsLeader)
            //    return;

            PluginLog.Information($"Received seek '{ms}' message.");
            MidiPlayerControl.ChangeTime(ms);
        }

    }
}
