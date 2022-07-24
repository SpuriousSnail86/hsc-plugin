using Dalamud.Logging;
using HSC.Managers.Ipc;
using HSC.Models.Settings;
using Melanchall.DryWetMidi.Interaction;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HSC.IPC
{/// <summary>
 /// sends message to external HSC windows app
 /// </summary>
 /// 
    internal partial class IpcManager
    {

        internal static void SendConnect()
        {
            SendMessage(new byte[] { (byte)MessageType.Client, (byte)ClientMessageType.Connect, (byte)Settings.CharIndex });
        }

        internal static void SendDisconnect()
        {
            SendMessage(new byte[] { (byte)MessageType.Client, (byte)ClientMessageType.Disconnect, (byte)Settings.CharIndex });
        }

        internal static void LoadStarted(int index)
        {
            if (!IpcManager.Connected)
                return;

            if (DalamudApi.api.PartyList.IsInParty() && !DalamudApi.api.PartyList.IsPartyLeader())
                return;

            var buffer = new byte[] { (byte)MessageType.Client, (byte)ClientMessageType.LoadStarted };
            buffer = buffer.Concat(BitConverter.GetBytes(index)).ToArray();
            SendMessage(buffer);
        }

        internal static void LoadFinished(int index, long length)
        {
            if (!IpcManager.Connected)
                return;

            if (DalamudApi.api.PartyList.IsInParty() && !DalamudApi.api.PartyList.IsPartyLeader())
                return;

            var buffer = new byte[] { (byte)MessageType.Client, (byte)ClientMessageType.LoadFinished };
            buffer = buffer.Concat(BitConverter.GetBytes(index)).ToArray();
            buffer = buffer.Concat(BitConverter.GetBytes(length)).ToArray();
            SendMessage(buffer);
        }


        internal static void PlaybackStarted(int index)
        {
            if (!IpcManager.Connected)
                return;

            if (DalamudApi.api.PartyList.IsInParty() && !DalamudApi.api.PartyList.IsPartyLeader())
                return;

            var buffer = new byte[] { (byte)MessageType.Client, (byte)ClientMessageType.PlaybackStarted };
            buffer = buffer.Concat(BitConverter.GetBytes(index)).ToArray();
            SendMessage(buffer);
        }

        internal static void PlaybackFinished()
        {
            if (!IpcManager.Connected)
                return;

            if (DalamudApi.api.PartyList.IsInParty() && !DalamudApi.api.PartyList.IsPartyLeader())
                return;

            SendMessage(new byte[] { (byte)MessageType.Client, (byte)ClientMessageType.PlaybackFinished });
        }

        internal static void Paused()
        {
            if (!IpcManager.Connected)
                return;

            if (DalamudApi.api.PartyList.IsInParty() && !DalamudApi.api.PartyList.IsPartyLeader())
                return;
            SendMessage(new byte[] { (byte)MessageType.Client, (byte)ClientMessageType.Paused });
        }

        internal static void Stopped()
        {
            if (!IpcManager.Connected)
                return;

            if (DalamudApi.api.PartyList.IsInParty() && !DalamudApi.api.PartyList.IsPartyLeader())
                return;

            SendMessage(new byte[] { (byte)MessageType.Client, (byte)ClientMessageType.Stopped });
        }

        internal static void Ticked()
        {
            if (!IpcManager.Connected)
                return;

            if (DalamudApi.api.PartyList.IsInParty() && !DalamudApi.api.PartyList.IsPartyLeader())
                return;

            var currentTime = HSC.CurrentPlayback.GetCurrentTime<MetricTimeSpan>();
            //PluginLog.Information($"Ticked: {currentTime.Minutes}:{currentTime.Seconds}");
            var buffer = new byte[] { (byte)MessageType.Client, (byte)ClientMessageType.Ticked, (byte)currentTime.Minutes, (byte)currentTime.Seconds };
            buffer = buffer.Concat(BitConverter.GetBytes(currentTime.TotalMicroseconds)).ToArray();
            SendMessage(buffer);
        }

        //public static void TimeChanged()
        //{

        //}
    }
}
