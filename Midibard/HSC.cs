using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Dalamud.Interface.Internal.Notifications;
using Dalamud.Logging;
using Dalamud.Plugin;
using Lumina.Data;
using Lumina.Data.Files;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;
using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Composing;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Multimedia;
using Melanchall.DryWetMidi.Interaction;
using Melanchall.DryWetMidi.MusicTheory;
using Melanchall.DryWetMidi.Standards;
using HSC.Control;
using HSC.Control.CharacterControl;
using HSC.Control.MidiControl;
using HSC.DalamudApi;
using HSC.Managers.Agents;
using HSC.Managers.Ipc;
using HSC.Util;
using static HSC.DalamudApi.api;
using Dalamud.Game.Gui;
using XivCommon;
using HSC.Config;
using HSC.Memory;
using HSC.IPC;
using System.Timers;
using HSC.Models.Settings;
using System.Threading;
using HSC.Helpers;

namespace HSC;

/// <summary>
/// author:  akira045, Ori, modified by SpuriousSnail86
/// </summary>
public partial class HSC : IDalamudPlugin
{

    internal static PlaybackDevice CurrentOutputDevice { get; set; }
    internal static MidiFile CurrentOpeningMidiFile { get; }
    internal static Playback CurrentPlayback { get; set; }
    internal static TempoMap CurrentTMap { get; set; }
    internal static Dictionary<int, TrackChunk> CurrentTracks { get; set; }
    internal static AgentMetronome AgentMetronome { get; set; }
    internal static AgentPerformance AgentPerformance { get; set; }
    //internal static AgentConfigSystem AgentConfigSystem { get; set; }


    internal static ExcelSheet<Perform> InstrumentSheet;

    public static global::HSC.Control.Instrument[] Instruments;

    internal static string[] InstrumentStrings;

    internal static IDictionary<SevenBitNumber, uint> ProgramInstruments;

    internal static byte CurrentInstrument => Marshal.ReadByte(Offsets.PerformanceStructPtr + 3 + Offsets.InstrumentOffset);
    internal static byte CurrentTone => Marshal.ReadByte(Offsets.PerformanceStructPtr + 3 + Offsets.InstrumentOffset + 1);
    internal static readonly byte[] guitarGroup = { 24, 25, 26, 27, 28 };
    internal static bool PlayingGuitar => guitarGroup.Contains(CurrentInstrument);

    internal static bool IsPlaying => CurrentPlayback?.IsRunning == true;

    public static System.Timers.Timer PlaybackTimer { get; set; }

    public string Name => "HSC";
    private static ChatGui _chatGui;

    public static XivCommonBase Cbase;
    public static bool pluginUnloading;

    public unsafe HSC(DalamudPluginInterface pi, ChatGui chatGui)
    {
        DalamudApi.api.Initialize(this, pi);

        InstrumentSheet = DataManager.Excel.GetSheet<Perform>();

        Instruments = InstrumentSheet!
            .Where(i => !string.IsNullOrWhiteSpace(i.Instrument) || i.RowId == 0)
            .Select(i => new global::HSC.Control.Instrument(i))
            .ToArray();

        InstrumentStrings = Instruments.Select(i => i.InstrumentString).ToArray();

        PluginLog.Information("<InstrumentStrings>");
        foreach (string s in InstrumentStrings)
        {
            PluginLog.Information(s);
        }
        PluginLog.Information("<InstrumentStrings \\>");

        ProgramInstruments = new Dictionary<SevenBitNumber, uint>();
        foreach (var (programNumber, instrument) in Instruments.Select((i, index) => (i.ProgramNumber, index)))
        {
            ProgramInstruments[programNumber] = (uint)instrument;
        }

        PluginLog.Information("<ProgramInstruments>");
        foreach (var programNumber in ProgramInstruments.Keys)
        {
            PluginLog.Information($"[{programNumber}] {ProgramNames.GetGMProgramName(programNumber)} {ProgramInstruments[programNumber]}");
        }
        PluginLog.Information("<ProgramInstruments \\>");

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        Configuration.Init();
        Configuration.Load();

        playlib.init(this);
        OffsetManager.Setup(api.SigScanner);
        GuitarTonePatch.InitAndApply();
        Cbase = new XivCommonBase();

        AgentMetronome = new AgentMetronome(AgentManager.Instance.FindAgentInterfaceByVtable(Offsets.MetronomeAgent));
        AgentPerformance = new AgentPerformance(AgentManager.Instance.FindAgentInterfaceByVtable(Offsets.PerformanceAgent));
        //AgentConfigSystem = new AgentConfigSystem(AgentManager.Instance.FindAgentInterfaceByVtable(Offsets.AgentConfigSystem));
        _ = EnsembleManager.Instance;

#if DEBUG
        _ = NetworkManager.Instance;
        _ = Testhooks.Instance;
#endif
        _chatGui = chatGui;
        _chatGui.ChatMessage += ChatCommand.OnChatMessage;

        CurrentOutputDevice = new PlaybackDevice();
        InputDeviceManager.ScanMidiDeviceThread.Start();

        //Framework.Update += MidiPlayerControl.Tick;

        //if (PluginInterface.IsDev) Ui.Open();

        DalamudApi.api.ClientState.Login -= ClientState_Login;
        DalamudApi.api.ClientState.Logout -= ClientState_Logout;
        DalamudApi.api.ClientState.Login += ClientState_Login;
        DalamudApi.api.ClientState.Logout += ClientState_Logout;

        if (DalamudApi.api.ClientState.IsLoggedIn || Configuration.config.OfflineTesting)
            Task.Run(() => HandleLogin());
    }

    #region public methods
    public static void StartTimer()
    {
        if (HSC.PlaybackTimer == null)
        {
            HSC.PlaybackTimer = new System.Timers.Timer();
            HSC.PlaybackTimer.Interval = 1000;
            HSC.PlaybackTimer.AutoReset = true;
            HSC.PlaybackTimer.Elapsed += PlaybackTimer_Elapsed;
        }
        if (!api.PartyList.IsInParty())
            HSC.PlaybackTimer?.Start();
        else if (api.PartyList.IsPartyLeader())
            HSC.PlaybackTimer?.Start();
    }


    public static void StopTimer()
    {
        if (!api.PartyList.IsInParty())
            HSC.PlaybackTimer?.Stop();
        else if (api.PartyList.IsPartyLeader())
            HSC.PlaybackTimer?.Stop();
    }

    public static void DoLockedWriteAction(System.Action action)
    {
        //configMutex = new Mutex(true, "MidiBard.Mutex");

        //configMutex.WaitOne();
        action();
        /*        configMutex.ReleaseMutex()*/
        ;
    }

    public static void DoLockedReadAction(System.Action action)
    {
        //configMutex = new Mutex(true, "MidiBard.Mutex");

        //configMutex.WaitOne();
        action();
        //configMutex.ReleaseMutex();
    }

    public static void PopulateConfigFromSettings()
    {
        if (!Settings.ConfigExists)
            return;

        Configuration.config.UseChordTrimming = Settings.AppSettings.GeneralSettings.EnableTrim;
        Configuration.config.UseTrimByTrack = Settings.AppSettings.GeneralSettings.EnableTrimFromTracks;
        Configuration.config.UseTransposing = Settings.AppSettings.GeneralSettings.EnableTranspose;
        Configuration.config.SwitchInstrumentFromPlaylist = Settings.AppSettings.GeneralSettings.EnableInstrumentSwitching;
        Configuration.config.UseCloseOnFinish = Settings.AppSettings.GeneralSettings.CloseOnFinish;
        Configuration.config.UseSendReadyCheck = Settings.AppSettings.GeneralSettings.SendReadyCheckOnEquip;
        Configuration.config.AutoPlaySong = Settings.AppSettings.GeneralSettings.AutoPlayOnSelect;
        Configuration.config.PlayMode = Settings.AppSettings.PlaylistSettings.RepeatMode;

        PluginLog.Information($"Updated config from HSC settings");
        //PluginLog.Information($"useChordTrimming: {Configuration.config.UseChordTrimming}");
        //PluginLog.Information($"useTrimByTrack: {Configuration.config.UseTrimByTrack}");
        //PluginLog.Information($"useTransposing: {Configuration.config.UseTransposing}");
        //PluginLog.Information($"switchInstrumentFromPlaylist: {Configuration.config.SwitchInstrumentFromPlaylist}");
        //PluginLog.Information($"useCloseOnFinish: {Configuration.config.UseCloseOnFinish}");
        //PluginLog.Information($"useSendReadyCheck: {Configuration.config.UseSendReadyCheck}");
        //PluginLog.Information($"AutoPlaySong: {Configuration.config.AutoPlaySong}");

        Configuration.Save();
    }

    public static void UpdateClientInfo()
    {
        if (Configuration.config.OfflineTesting)
        {
            Settings.CharName = "TEST";
            Settings.CharIndex = 0;
        }
        else
        {
            while (string.IsNullOrEmpty(Settings.CharName))
            {
                try
                {
                    Settings.CharName = DalamudApi.api.ClientState.LocalPlayer?.Name.TextValue;
                    Thread.Sleep(1000);
                }
                catch (Exception ex)
                {

                }
            }
            CharConfigHelpers.UpdateCharIndex(Settings.CharName);
        }

        PluginLog.Information($"Client logged in. HSC client info - index: {Settings.CharIndex}, character name: '{Settings.CharName}'.");
    }
    #endregion

    #region private methods
    private static void HandleLogin(bool loggedIn = false)
    {
        Settings.CurrentAppPath = DalamudApi.api.PluginInterface.AssemblyLocation.DirectoryName;
        PluginLog.Information($"Current plugin path '{Settings.CurrentAppPath}'.");

        RunBackgroundThread();

        ConfigWatcher.Create();
        Settings.Load();
        HSC.PopulateConfigFromSettings();
        HSC.UpdateClientInfo();

        PlaylistManager.Reload(loggedIn);
        PlaylistManager.ReloadSettingsAndSwitch(loggedIn);

        IpcManager.ClientExited += IpcManager_ClientExited;
        IpcManager.Init();
    }

    private static void IpcManager_ClientExited(object sender, EventArgs e)
    {
        IpcManager.Stop();
        MidiPlayerControl.Stop();
        StopTimer();
    }

    private static void RunBackgroundThread()
    {
        Task.Run(() =>
        {
            while (!pluginUnloading && api.ClientState.IsLoggedIn)
            {
                if (AgentPerformance.InPerformanceMode && api.PartyList.IsInParty())
                {
                    if (!api.PartyList.IsPartyLeader())
                        playlib.ConfirmReceiveReadyCheck();

                }
                Thread.Sleep(200);
            }
        });
    }
    #endregion

    #region events
    private static void PlaybackTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
    {
        if (!api.PartyList.IsInParty())
            IpcManager.Ticked();
        else if (api.PartyList.IsPartyLeader())
            IpcManager.Ticked();
    }

    private static void ClientState_Logout(object sender, EventArgs e)
    {
        Cleanup();
    }

    private static void ClientState_Login(object sender, EventArgs e)
    {
        Task.Run(() => HandleLogin(true));
    }
    #endregion

    #region commands
    [Command("/hsc")]
    [HelpMessage("Toggle HarpSEchord in-game UI")]
    public void Command1(string command, string args) => OnCommand(command, args);

    [Command("/hsc")]
    [HelpMessage("toggle HarpSEchord in-game UI\n" +
                 "/mbard perform [instrument name|instrument ID] → switch to specified instrument\n" +
                 "/mbard cancel → quit performance mode\n" +
                 "/mbard visual [on|off|toggle] → midi tracks visualization\n" +
                 "/mbard [play|pause|playpause|stop|next|prev|rewind (seconds)|fastforward (seconds)] → playback control" +
                 "Party commands: Type commands below on party chat to control all bards in the party.\n" +
                 "switchto <track number> → Switch to <track number> on the play list. e.g. switchto 3 = Switch to the 3rd song.\n" +
                 "close → Stop playing and exit perform mode.\n" +
                 "reloadplaylist → Reload playlist on all clients from the same PC, use after making any changes on the playlist.")]
    public void Command2(string command, string args) => OnCommand(command, args);

    void OnCommand(string command, string args)
    {
        var argStrings = args.ToLowerInvariant().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        PluginLog.Debug($"command: {command}, {string.Join('|', argStrings)}");
        if (argStrings.Any())
        {
            switch (argStrings[0])
            {
                case "cancel":
                    PerformActions.DoPerformAction(0);
                    break;
                //case "perform":
                //    try
                //    {
                //        var instrumentInput = argStrings[1];
                //        if (instrumentInput == "cancel")
                //        {
                //            PerformActions.DoPerformAction(0);
                //        }
                //        else if (uint.TryParse(instrumentInput, out var id1) && id1 < InstrumentStrings.Length)
                //        {
                //            SwitchInstrument.SwitchToContinue(id1);
                //        }
                //        else if (SwitchInstrument.TryParseInstrumentName(instrumentInput, out var id2))
                //        {
                //            SwitchInstrument.SwitchToContinue(id2);
                //        }
                //    }
                //    catch (Exception e)
                //    {
                //        PluginLog.Warning(e, "error when parsing or finding instrument strings");
                //        _chatGui.PrintError($"failed parsing command argument \"{args}\"");
                //    }

                //    break;
                case "playpause":
                    MidiPlayerControl.PlayPause();
                    break;
                case "play":
                    MidiPlayerControl.Play();
                    break;
                case "pause":
                    MidiPlayerControl.Pause();
                    break;
                case "stop":
                    MidiPlayerControl.Stop();
                    break;
                case "next":
                    MidiPlayerControl.Next();
                    break;
                case "prev":
                    MidiPlayerControl.Prev();
                    break;
                case "rewind":
                    {
                        double timeInSeconds = -5;
                        try
                        {
                            timeInSeconds = -double.Parse(argStrings[1]);
                        }
                        catch (Exception e)
                        {
                        }

                        MidiPlayerControl.MoveTime(timeInSeconds);
                    }
                    break;
                case "fastforward":
                    {
                        double timeInSeconds = 5;
                        try
                        {
                            timeInSeconds = double.Parse(argStrings[1]);
                        }
                        catch (Exception e)
                        {
                        }

                        MidiPlayerControl.MoveTime(timeInSeconds);
                    }
                    break;
            }
        }
        else
        {

        }
    }
    #endregion

    #region IDisposable Support
    public void Dispose()
    {
        _chatGui.ChatMessage -= ChatCommand.OnChatMessage;
        Cbase.Dispose();
        FreeUnmanagedResources();
        GC.SuppressFinalize(this);
    }

    private static void Cleanup()
    {
        try
        {
            _chatGui.ChatMessage -= ChatCommand.OnChatMessage;
            Cbase.Dispose();

            pluginUnloading = true;

            ConfigWatcher.Dispose();
            IpcManager.Dispose();
            GuitarTonePatch.Dispose();
            InputDeviceManager.ShouldScanMidiDeviceThread = false;
            //Framework.Update -= MidiPlayerControl.Tick;

            EnsembleManager.Instance.Dispose();
            InputDeviceManager.DisposeCurrentInputDevice();

            try
            {
                CurrentPlayback?.Stop();
                CurrentPlayback?.Dispose();
                CurrentPlayback = null;
            }
            catch (Exception e)
            {
                PluginLog.Error($"{e}");
            }
            DalamudApi.api.Dispose();

            PlaybackTimer?.Stop();
            PlaybackTimer?.Dispose();
            PlaybackTimer = null;
        }
        catch (Exception e2)
        {
            PluginLog.Error(e2, "error when disposing midibard");
        }
    }

    void FreeUnmanagedResources()
    {
        try
        {
            pluginUnloading = true;

            ConfigWatcher.Dispose();
            IpcManager.Dispose();
            GuitarTonePatch.Dispose();
            InputDeviceManager.ShouldScanMidiDeviceThread = false;
            //Framework.Update -= MidiPlayerControl.Tick;

            EnsembleManager.Instance.Dispose();
#if DEBUG
            Testhooks.Instance?.Dispose();
			NetworkManager.Instance.Dispose();
#endif
            InputDeviceManager.DisposeCurrentInputDevice();
            try
            {
                CurrentPlayback?.Stop();
                CurrentPlayback?.Dispose();
                CurrentPlayback = null;
            }
            catch (Exception e)
            {
                PluginLog.Error($"{e}");
            }
            DalamudApi.api.Dispose();

            PlaybackTimer?.Stop();
            PlaybackTimer?.Dispose();
            PlaybackTimer = null;
        }
        catch (Exception e2)
        {
            PluginLog.Error(e2, "error when disposing midibard");
        }
    }

    ~HSC()
    {
        FreeUnmanagedResources();
    }
    #endregion
}