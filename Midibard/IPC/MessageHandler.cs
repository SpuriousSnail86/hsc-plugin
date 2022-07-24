using Dalamud.Logging;
using HSC.Helpers;
using HSC.Midi;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TinyIpc.Messaging;

namespace HSC.IPC
{
    /// <summary>
    /// author:  SpuriousSnail86
    /// </summary>
    internal partial class MessageHandler
    {
        public static event EventHandler<int> ConnectMessageReceived;
        public static event EventHandler<int> DisconnectMessageReceived;

        public static event EventHandler ReloadPlaylistMessageReceived;
        public static event EventHandler ReloadPlaylistSettingsMessageReceived;
        public static event EventHandler<int> ChangeSongMessageReceived;
        public static event EventHandler SwitchInstrumentsMessageReceived;
        public static event EventHandler ClosePerformanceMessageReceived;

        public static event EventHandler PlayMessageReceived;
        public static event EventHandler PauseMessageReceived;
        public static event EventHandler StopMessageReceived;
        public static event EventHandler NextMessageReceived;
        public static event EventHandler PreviousMessageReceived;
        public static event EventHandler<float> ChangeSpeedMessageReceived;
        public static event EventHandler<long> SeekMessageReceived;

        public static void Start()
        {
            ConnectMessageReceived += OnConnectMessageReceived;
            DisconnectMessageReceived += OnDisconnectMessageReceived;

            ChangeSongMessageReceived += OnChangeSongMessageReceived;
            ReloadPlaylistMessageReceived += OnReloadPlaylistMessageReceived;
            ReloadPlaylistSettingsMessageReceived += OnReloadPlaylistSettingsMessageReceived;
            SwitchInstrumentsMessageReceived += OnSwitchInstrumentsMessageReceived;
            ClosePerformanceMessageReceived += OnClosePerformanceMessageReceived;

            PlayMessageReceived += OnPlayMessageReceived;
            PauseMessageReceived += OnPauseMessageReceived;
            StopMessageReceived += OnStopMessageReceived;
            NextMessageReceived += OnNextMessageReceived;
            PreviousMessageReceived += OnPreviousMessageReceived;
            ChangeSpeedMessageReceived += OnChangeSpeedMessageReceived;
            SeekMessageReceived += OnSeekMessageReceived;
        }


        public static void Stop()
        {
            ConnectMessageReceived -= OnConnectMessageReceived;
            DisconnectMessageReceived -= OnDisconnectMessageReceived;

            ChangeSongMessageReceived -= OnChangeSongMessageReceived;
            ReloadPlaylistMessageReceived -= OnReloadPlaylistMessageReceived;
            ReloadPlaylistSettingsMessageReceived -= OnReloadPlaylistSettingsMessageReceived;
            SwitchInstrumentsMessageReceived -= OnSwitchInstrumentsMessageReceived;
            ClosePerformanceMessageReceived -= OnClosePerformanceMessageReceived;

            PlayMessageReceived -= OnPlayMessageReceived;
            PauseMessageReceived -= OnPauseMessageReceived;
            StopMessageReceived -= OnStopMessageReceived;
            NextMessageReceived -= OnNextMessageReceived;
            PreviousMessageReceived -= OnPreviousMessageReceived;
            ChangeSpeedMessageReceived -= OnChangeSpeedMessageReceived;
            SeekMessageReceived -= OnSeekMessageReceived;
        }

        public static void HandleMessage(byte[] buffer)
        {
            try
            {
                if (buffer.Length == 0)
                    return;

                ProcessMessage(buffer);
            }
            catch (Exception ex)
            {
                PluginLog.Error($"Error processing IPC message buffer: {ex.Message}");
            }
        }

        private static void ProcessMessage(byte[] buffer)
        {
            var ms = new MemoryStream(buffer);
            var br = new BinaryReader(ms);

            var type = (MessageType)br.ReadByte();

            switch(type)
            {
                case MessageType.ReloadPlaylist:
                case MessageType.ReloadPlaylistSettings:
                case MessageType.SwitchInstruments:
                case MessageType.Close:
                    HandleMessageNoArgs(type);
                    break;
                default:
                    HandleMessageArgs(type, buffer.Skip(1).ToArray());
                    break;
            }

            br.Close();
            ms.Close();
        }

        private static void HandleMessageNoArgs(MessageType type)
        {
            switch (type)
            {

                case MessageType.ReloadPlaylist:
                    ReloadPlaylistMessageReceived.Invoke(null, EventArgs.Empty);
                    break;

                case MessageType.ReloadPlaylistSettings:
                    ReloadPlaylistSettingsMessageReceived.Invoke(null, EventArgs.Empty);
                    break;

                case MessageType.SwitchInstruments:
                    SwitchInstrumentsMessageReceived.Invoke(null, EventArgs.Empty);
                    break;

                case MessageType.Close:
                    ClosePerformanceMessageReceived.Invoke(null, EventArgs.Empty);
                    break;
            }

        }


        private static void HandleMessageArgs(MessageType type, byte[] buffer)
        {
            var ms = new MemoryStream(buffer);
            var br = new BinaryReader(ms);

            switch (type)
            {

                case MessageType.Connect:
                    int index = br.ReadByte();
                    ConnectMessageReceived.Invoke(null, index);
                    break;
                case MessageType.Disconnect:
                    index = br.ReadByte();
                    DisconnectMessageReceived.Invoke(null, index);
                    break;
                case MessageType.ChangeSong:
                    int songIndex = br.ReadInt32();
                    ChangeSongMessageReceived.Invoke(null, songIndex);
                    break;
                case MessageType.Playback:
                    var msgType = (PlaybackMessageType)br.ReadByte();
                    HandlePlaybackMessage(msgType, buffer.Skip(1).ToArray());
                    break;
            }

            br.Close();
            ms.Close();
        }

        private static void HandlePlaybackMessage(PlaybackMessageType type, byte[] buffer)
        {
            var ms = new MemoryStream(buffer);
            var br = new BinaryReader(ms);

            switch (type)
            {

                case PlaybackMessageType.Play:
                    PlayMessageReceived.Invoke(null, EventArgs.Empty);
                    break;

                case PlaybackMessageType.Pause:
                    PauseMessageReceived.Invoke(null, EventArgs.Empty);
                    break;

                case PlaybackMessageType.Stop:
                    StopMessageReceived.Invoke(null, EventArgs.Empty);
                    break;

                case PlaybackMessageType.Next:
                   NextMessageReceived.Invoke(null, EventArgs.Empty);
                    break;

                case PlaybackMessageType.Previous:
                    PreviousMessageReceived.Invoke(null, EventArgs.Empty);
                    break;

                case PlaybackMessageType.ChangeSpeed:
                    double speed = br.ReadDouble();
                    ChangeSpeedMessageReceived.Invoke(null, (float)speed);
                    break;


                case PlaybackMessageType.Seek:
                    long msTime = br.ReadInt64();
                    SeekMessageReceived.Invoke(null, msTime);
                    break;
            }

            br.Close();
            ms.Close();

        }
    }
}

