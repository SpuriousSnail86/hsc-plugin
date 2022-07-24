
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Interaction;
using Newtonsoft.Json;
using Dalamud.Logging;

using HSC.Models.Music;
using HSC.Helpers;
using HSC.Config;
using HSC.Managers.Ipc;

namespace HSC.Models.Settings
{
    /// <summary>
    /// author:  SpuriousSnail86
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public static class Settings
    {
        public const string HSCSettingsFileName = "settings.config";



        public class TrackTransposeInfo
        {
            public int KeyOffset { get; set; }
            public int OctaveOffset { get; set; }
        }

        static Settings()
        {

            PlaylistSettings = new SongSettings();

            Playlist = new Models.Playlist.Playlist();

            AppSettings = new AppSettings();

            CharConfig = new CharacterConfig();
        }

        public static void Cleanup()
        {
            CharName = null;
            CharIndex = -1;

            Settings.Playlist?.Clear();
            Settings.PlaylistSettings?.Clear();

            PercussionNotes?.Clear();
            PercussionTracks?.Clear();
            MappedTracks?.Clear();
            TrackInfo?.Clear();
        }

        [JsonProperty]
        public static AppSettings AppSettings { get; set; }

        public static Models.Playlist.Playlist Playlist { get; set; }

        public static SongSettings PlaylistSettings { get; set; }

        public static CharacterConfig CharConfig { get; set; }

        public static Dictionary<int, Dictionary<int, bool>> PercussionNotes { get; set; }
        public static Dictionary<int, bool> PercussionTracks { get; set; }

        public static Dictionary<int, TrackTransposeInfo> MappedTracks { get; set; }

        public static Dictionary<int, TrackTransposeInfo> TrackInfo { get; set; }

        public static Dictionary<long, Dictionary<SevenBitNumber, bool>> TrimmedNotes { get; set; }

        public static Dictionary<int, bool> LeaderTracks { get; set; }

        public static Dictionary<int, int> ClientIndexes { get; set; }

        public static Dictionary<int, bool> EnabledTracks { get; set; }

        public static MidiSequence CurrentSongSettings { get; set; }

        public static bool IsLeader =>  DalamudApi.api.PartyList.IsInParty() && DalamudApi.api.PartyList.IsPartyLeader();
        public static bool SavedConfig { get; set; }
        public static bool ConfigExists { get; set; }

        public static string CurrentAppPath { get; set; }
        public static string CharName { get; set; }

        public static int CharIndex { get; set; }
        public static int OctaveOffset { get; set; }
        public static int KeyOffset { get; set; }

        public static ITimeSpan PrevTime { get; set; }
        public static bool SwitchInstrumentFailed { get; internal set; }

        public static void Load()
        {

            var filePath = Path.Combine(DalamudApi.api.PluginInterface.GetPluginConfigDirectory(), HSCSettingsFileName);
            PluginLog.LogDebug($"Load HSC Setting: {filePath}");
            var appSettings = FileHelpers.Load<AppSettings>(filePath);

            if (appSettings != null)
            {
                AppSettings = appSettings;
                ConfigExists = true;
            }
            else
            {
                PluginLog.LogDebug($"HSC AppSettings not exist: {filePath}");
                ConfigExists = false;
            }
        }

        public static void Save()
        {
            SavedConfig = true;

            var filePath = Path.Combine(DalamudApi.api.PluginInterface.GetPluginConfigDirectory(), HSCSettingsFileName);
            PluginLog.LogDebug($"Save HSC Setting: {filePath}");
            FileHelpers.Save(AppSettings, filePath);

        }

    }
}
