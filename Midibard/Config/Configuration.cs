using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Dalamud;
using Dalamud.Configuration;
using Dalamud.Logging;
using Dalamud.Plugin;
using ImGuiNET;
using HSC.Common;
using Newtonsoft.Json;
using HSC.Helpers;
using HSC.Models.Settings;

namespace HSC.Config;

public enum PlayMode
{
    Single,
    SingleRepeat,
    ListOrdered,
    ListRepeat,
    Random
}

public enum GuitarToneMode
{
    Off,
    Standard,
    Simple,
    Override,
}
public enum UILang
{
    EN,
    CN
}

[JsonObject(MemberSerialization.OptOut)]

/// <summary>
/// author: akira045/Ori, modified by SpuriousSnail86
/// </summary>
public class Configuration
{

    static Configuration()
    {
        config = new Configuration();
    }

    public static Configuration config;


    public bool switchInstrumentFromPlaylist = true;
    public bool useChordTrimming = true;
    public bool useTransposing = true;
    public bool useTrimByTrack = false;
    public bool useCloseOnFinish = false;
    public bool useSendReadyCheck = false;
    public bool autoPlaySong = false;
    public int ipcDelay = 5000;
    public bool offlineTesting = false;
    public bool useSongCache = true;

    public string hscMidiFile;

    public int prevSelected;

    public int version;

    public bool debug;
    public bool debugAgentInfo;
    public bool debugDeviceInfo;
    public bool debugOffsets;
    public bool debugKeyStroke;
    public bool debugMisc;
    public bool debugEnsemble;

    public List<string> playlist = new List<string>();

    public float playSpeed = 1f;
    public float secondsBetweenTracks = 3;
    public int playMode = 0;
    public int transposeGlobal = 0;
    public bool autoAdaptNotes = true;

    public bool monitorOnEnsemble = true;
    public int[] tonesPerTrack = new int[100];
    public int uiLang = DalamudApi.api.PluginInterface.UiLanguage == "zh" ? 1 : 0;


    public bool autoSwitchInstrumentBySongName = true;
    public bool autoTransposeBySongName = true;

    public bool bmpTrackNames = false;

    public bool lazyNoteRelease = true;
    public string lastUsedMidiDeviceName = "";
    public bool autoRestoreListening = false;
    public string lastOpenedFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);


    public bool stopPlayingWhenEnsembleEnds = false;

    public GuitarToneMode guitarToneMode = GuitarToneMode.Off;
    public int switchInstrumentDelay = 3000;

    private int ensembleMessageQueueMax = 100;
    private int ensembleTimerInterval = 3000;
    private bool useEnsembleMsgQueue = false;
    private bool useEnsembleMsgQueueTimer = false;

    public bool overrideGuitarTones => GuitarToneMode == GuitarToneMode.Override;

    public static string ConfigFilePath => Path.Combine(DalamudApi.api.PluginInterface.GetPluginConfigDirectory(), $"{Assembly.GetCallingAssembly().GetName().Name}.json");

    public int Version { get => version; set => version = value; }
    public bool Debug { get => debug; set => debug = value; }
    public bool DebugAgentInfo { get => debugAgentInfo; set => debugAgentInfo = value; }
    public bool DebugDeviceInfo { get => debugDeviceInfo; set => debugDeviceInfo = value; }
    public bool DebugOffsets { get => debugOffsets; set => debugOffsets = value; }
    public bool DebugKeyStroke { get => debugKeyStroke; set => debugKeyStroke = value; }
    public bool DebugMisc { get => debugMisc; set => debugMisc = value; }
    public bool DebugEnsemble { get => debugEnsemble; set => debugEnsemble = value; }

    public bool SwitchInstrumentFromPlaylist { get => switchInstrumentFromPlaylist; set => switchInstrumentFromPlaylist = value; }
    public bool UseChordTrimming { get => useChordTrimming; set => useChordTrimming = value; }
    public bool UseTransposing { get => useTransposing; set => useTransposing = value; }
    public bool UseTrimByTrack { get => useTrimByTrack; set => useTrimByTrack = value; }
    public bool UseCloseOnFinish { get => useCloseOnFinish; set => useCloseOnFinish = value; }
    public bool UseSendReadyCheck { get => useSendReadyCheck; set => useSendReadyCheck = value; }
    public bool AutoPlaySong { get => autoPlaySong; set => autoPlaySong = value; }

    public int EnsembleMessageQueueMax { get => ensembleMessageQueueMax; set => ensembleMessageQueueMax = value; }
    public bool UseEnsembleMsgQueue { get => useEnsembleMsgQueue; set => useEnsembleMsgQueue = value; }
    public bool UseEnsembleMsgQueueTimer { get => useEnsembleMsgQueueTimer; set => useEnsembleMsgQueueTimer = value; }
    public int EnsembleTimerInterval { get => ensembleTimerInterval; set => ensembleTimerInterval = value; }

    public bool UseSongCache { get => useSongCache; set => useSongCache = value; }
    public string HscMidiFile { get => hscMidiFile; set => hscMidiFile = value; }

    public int IpcDelay { get => ipcDelay; set => ipcDelay = value; }

    public bool OfflineTesting { get => offlineTesting; set => offlineTesting = value; }

    public int PrevSelected { get => prevSelected; set => prevSelected = value; }

    public List<string> Playlist { get => playlist; set => playlist = value; }
    public float PlaySpeed { get => playSpeed; set => playSpeed = value; }
    public float SecondsBetweenTracks { get => secondsBetweenTracks; set => secondsBetweenTracks = value; }
    public int PlayMode { get => playMode; set => playMode = value; }
    public int TransposeGlobal { get => transposeGlobal; set => transposeGlobal = value; }
    public bool AutoAdaptNotes { get => autoAdaptNotes; set => autoAdaptNotes = value; }
    public bool MonitorOnEnsemble { get => monitorOnEnsemble; set => monitorOnEnsemble = value; }
    public int[] TonesPerTrack { get => tonesPerTrack; set => tonesPerTrack = value; }
    public int UiLang { get => uiLang; set => uiLang = value; }
    public bool AutoSwitchInstrumentBySongName { get => autoSwitchInstrumentBySongName; set => autoSwitchInstrumentBySongName = value; }
    public bool AutoTransposeBySongName { get => autoTransposeBySongName; set => autoTransposeBySongName = value; }
    public bool BmpTrackNames { get => bmpTrackNames; set => bmpTrackNames = value; }
    public bool LazyNoteRelease { get => lazyNoteRelease; set => lazyNoteRelease = value; }
    public string LastUsedMidiDeviceName { get => lastUsedMidiDeviceName; set => lastUsedMidiDeviceName = value; }
    public bool AutoRestoreListening { get => autoRestoreListening; set => autoRestoreListening = value; }
    public string LastOpenedFolderPath { get => lastOpenedFolderPath; set => lastOpenedFolderPath = value; }
    public bool StopPlayingWhenEnsembleEnds { get => stopPlayingWhenEnsembleEnds; set => stopPlayingWhenEnsembleEnds = value; }
    public GuitarToneMode GuitarToneMode { get => guitarToneMode; set => guitarToneMode = value; }
    public int SwitchInstrumentDelay { get => switchInstrumentDelay; set => switchInstrumentDelay = value; }



    public static void Init()
    {
        ConfigurationPrivate.Init();
    }

    public static void Save(bool reloadplaylist = false)
    {

        try
        {
            if (!Settings.IsLeader)//only leader saves global config
                return;

            var startNew = Stopwatch.StartNew();

            HSC.DoLockedWriteAction(() =>
            {
                FileHelpers.Save(config, ConfigFilePath);
                ConfigurationPrivate.config.Save();
            });

        }
        catch (Exception e)
        {
            PluginLog.Error(e, "Error when saving config");
        }

    }

    public static void Load()
    {
        HSC.DoLockedReadAction(() => config = FileHelpers.Load<Configuration>(ConfigFilePath) ?? new Configuration());
        LoadPrivate();
    }

    public static void LoadPrivate()
    {
        HSC.DoLockedReadAction(() => ConfigurationPrivate.Load());
    }
}