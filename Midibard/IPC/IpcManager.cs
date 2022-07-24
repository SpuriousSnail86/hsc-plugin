using Dalamud.Interface.Internal.Notifications;
using Dalamud.Logging;
using SharedMemory;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HSC.Common;
using HSC.Config;
using HSC.Models.Settings;
using HSC.Helpers;
using TinyIpc.Messaging;
using HSC.Managers.Ipc;

namespace HSC.IPC
{
    /// <summary>
    /// author:  SpuriousSnail86
    /// </summary>
    public enum MessageType { //from HSC app to plugin
        ReloadPlaylist = 1, 
        ReloadPlaylistSettings = 2, 
        ChangeSong = 3, 
        SwitchInstruments = 4, 
        OpenInstrument = 5, //for other clients broadcasted from leader
        Close = 6,
        Midi = 7,
        Playback = 8,
        Client = 9,
        Connect = 10,
        Disconnect = 11
    }
    public enum ClientMessageType //from plugin to HSC app
    {
        PlaybackStarted = 1,
        PlaybackFinished = 2,
        Stopped = 3,
        Paused = 4,
        Ticked = 5,
        LoadStarted = 6,
        LoadFinished = 7,
        Connect = 8,
        Disconnect = 9
    }

    public enum PlaybackMessageType
    {
        Play = 1,
        Pause = 2,
        Stop = 3,
        Next = 4,
        Previous = 5,
        Seek = 6,
        ChangeSpeed = 7
    }

    internal partial class IpcManager
    {

        const string MessageBusName = "HSC.MessageBus";

        static TinyIpc.Messaging.TinyMessageBus bus;

        private static bool hscScannerStarted;

        private static bool shouldStart;

        public static bool Connected { get; private set; }

        public static event EventHandler ClientExited;
        public static event EventHandler ClientFound;
        public static event EventHandler ClientConnected;
        public static event EventHandler ClientDisconnected;

        internal static void Init(bool loggedIn = false)
        {
            try
            {
                PluginLog.Information("Starting HSC IPC.");
                shouldStart = true;
                Task.Run(() => StartHscScanner());
                PluginLog.Information("HSC IPC started success.");
            }
            catch (Exception ex)
            {
                PluginLog.Error($"An error occured starting HSC IPC. Message: {ex.Message}");
            }
        }

        internal static void Disconnect()
        {
            shouldStart = false;
            Connected = false;
            ClientDisconnected.Invoke(null, EventArgs.Empty);
            ImGuiUtil.AddNotification(NotificationType.Error, $"Disconnected.");
        }

        internal static void Connect()
        {
            shouldStart = true;
            Connected = true;
            ClientConnected.Invoke(null, EventArgs.Empty);
            ImGuiUtil.AddNotification(NotificationType.Success, $"Connected.");
        }


        internal static void Dispose()
        {
            try
            {
                PluginLog.Information($"Stopping HSC IPC and cleaning up.");
                hscScannerStarted = false;
                Stop();
            }
            catch (Exception ex)
            {
                PluginLog.Error($"An error occured on HSC IPC cleanup. Message: {ex.Message}");
            }
        }

        internal static void Stop()
        {
            try
            {
                SendDisconnect();
                Connected = false;
                bus?.Dispose();
                MessageHandler.Stop();
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "error stopping IPC manager");
            }
        }

        internal static void Start()
        {
            try
            {
                //ImGuiUtil.AddNotification(NotificationType.Info, $"Connecting...");
                bus = new TinyIpc.Messaging.TinyMessageBus(MessageBusName);
                bus.MessageReceived -= HandleMessageReceived;
                bus.MessageReceived += HandleMessageReceived;
                Connected = true;
                SendConnect();
                MessageHandler.Start();
                PluginLog.Information($"Started IPC manager. handling messages.");
                ImGuiUtil.AddNotification(NotificationType.Success, $"Connected.");
            }
            catch (Exception ex)
            {
                ImGuiUtil.AddNotification(NotificationType.Error, $"Failed to connect.");
                PluginLog.Error(ex, "error starting IPC manager");
            }
        }

        private static void SendMessage(byte[] msg)
        {
            try
            {
                bus?.PublishAsync(msg);
            }

            catch (Exception ex)
            {
                PluginLog.Error(ex, "error sending IPC message");
            }
        }

        private static void HandleMessageReceived(object sender, TinyMessageReceivedEventArgs args)
        {
            MessageHandler.HandleMessage(args.Message);
        }

        private static void StartHscScanner()
        {
            try
            {
                if (hscScannerStarted)
                    return;

                hscScannerStarted = true;

                while (hscScannerStarted && DalamudApi.api.ClientState.IsLoggedIn || Configuration.config.OfflineTesting)
                {
                    if (!hscScannerStarted)
                    {
                        PluginLog.Information($"Stopping HSC scanner.");
                        break;
                    }

                    bool found = !ProcessFinder.Find("HSC").IsNullOrEmpty();

                    if (!found)
                    {
                        if (Connected)
                        {
                            PluginLog.Information("HSC exited. stopping.");
                            Stop();
                            ClientExited.Invoke(null, EventArgs.Empty);
                        }
                    }

                    if (found)
                    {
                        if (!Connected && shouldStart)
                        {
                            Start();
                            ClientFound.Invoke(null, EventArgs.Empty);
                        }
                    }
                    Thread.Sleep(1000);
                }
            }
            catch(Exception ex)
            {
                PluginLog.Error(ex, "Error scanning HSC");
                //ImGuiUtil.AddNotification(NotificationType.Error, $"Failed to connect to HSC");
            }
        }

    }
}
