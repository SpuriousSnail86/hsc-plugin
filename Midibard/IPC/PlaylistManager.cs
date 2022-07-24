using Dalamud.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HSC.Common;

using Dalamud.Interface.Internal.Notifications;
using HSC.Config;
using static HSC.Models.Settings.Settings;
using HSC.Models.Music;
using HSC.Models.Settings;
using HSC.Helpers;

namespace HSC.IPC
{
    /// <summary>
    /// author:  SpuriousSnail86
    /// </summary>
    public class PlaylistManager
    {
        private static bool wasPlaying;

        private static void UpdatePercussionNote(int trackIndex, int note)
        {
            if (!Settings.PercussionNotes.ContainsKey(trackIndex))
                Settings.PercussionNotes[trackIndex] = new Dictionary<int, bool>() { { note, true } };
            else
                Settings.PercussionNotes[trackIndex].Add(note, true);
        }


        private static void UpdateMappedTracks(int parentIndex, TrackTransposeInfo info)
        {
            if (!Settings.MappedTracks.ContainsKey(parentIndex))
                Settings.MappedTracks.Add(parentIndex, info);
        }

        private static bool IsTrackAssigned(Track track) => Settings.AppSettings.TrackSettings.PopulateFromPlaylist ? track.EnsembleMember.HasValue : track.AutofilledMember.HasValue;

        private static int GetTrackClientIndex(Track track) => Settings.AppSettings.TrackSettings.PopulateFromPlaylist ? track.EnsembleMember.Value : track.AutofilledMember.Value;

        private static bool TrackCharIndexChanged(Track track) => (Settings.AppSettings.TrackSettings.PopulateFromPlaylist ? track.EnsembleMember : track.AutofilledMember) == Settings.CharIndex;

        private static void ClearTracks()
        {
            if (!Settings.EnabledTracks.IsNullOrEmpty())
                Settings.EnabledTracks.Clear();
        }

        private static void UpdateTracks(MidiSequence seq)
        {
            if (seq.Tracks.IsNullOrEmpty())
                return;

            PluginLog.Information($"Updating tracks of '{Settings.AppSettings.CurrentSong}' from HSC playlist.");

            Settings.PercussionNotes = new Dictionary<int, Dictionary<int, bool>>();
            Settings.MappedTracks = new Dictionary<int, TrackTransposeInfo>();
            Settings.TrackInfo = new Dictionary<int, TrackTransposeInfo>();
            Settings.LeaderTracks = new Dictionary<int, bool>();
            Settings.ClientIndexes = new Dictionary<int, int>();
            Settings.EnabledTracks = new Dictionary<int, bool>();

            var tracks = seq.Tracks.ToArray();

            Parallel.ForEach(tracks, track =>
            {
                int index = track.Key;

                var info = new Settings.TrackTransposeInfo() { KeyOffset = track.Value.KeyOffset, OctaveOffset = track.Value.OctaveOffset };

                if (!Settings.TrackInfo.ContainsKey(index))
                    Settings.TrackInfo.Add(index, (TrackTransposeInfo)info);

                if (Configuration.config.overrideGuitarTones && PerformanceHelpers.HasGuitar((Track)track.Value))
                    Configuration.config.TonesPerTrack[index] = PerformanceHelpers.GetGuitarTone((Track)track.Value);

                //int clientIndex = GetTrackClientIndex(track.Value);

                //if (!track.Value.Muted && IsTrackAssigned(track.Value))
                //{
                //    Settings.EnabledTracks.Add(track.Key, true);
                //    //Settings.ClientIndexes.Add(track.Key, clientIndex);
                //}

                if (!track.Value.Muted && TrackCharIndexChanged((Track)track.Value))
                {
                    //LeaderTracks.Add(track.Key, true);
                    Settings.EnabledTracks.Add(track.Key, true);
                    //if (track.Value.PercussionNote.HasValue && track.Value.ParentIndex.HasValue)
                    //{
                    //    PluginLog.Information($"Percussion track {index} ({track.Value.PercussionNote.Value}) has parent {track.Value.ParentIndex} from HSC playlist");
                    //    UpdatePercussionNote((int)track.Value.ParentIndex.Value, (int)track.Value.PercussionNote.Value);
                    //}

                    //PluginLog.Information($"Track {index} is assigned from HSC playlist");

                    //percussion + duplication logic. if track has parent enable its parent
                    //if (track.Value.ParentIndex.HasValue)
                    //{
                    //    ConfigurationPrivate.config.EnabledTracks[(int)track.Value.ParentIndex.Value] = true;

                    //    PlaylistManager.UpdateMappedTracks((int)track.Value.ParentIndex.Value, (Settings.TrackTransposeInfo)info);
                    //}
                    //else//no parent enable as normal
                    //    ConfigurationPrivate.config.EnabledTracks[index] = true;
                }
            });

            foreach(var index in Settings.ClientIndexes)
            {
                PluginLog.Information($"Client index: {index.Key} - {index.Value}");
            }
        }

        private static void OpenPlaylist()
        {
            string path = DalamudApi.api.PluginInterface.GetPluginConfigDirectory();

            var files = Directory.GetFiles(path, "*.pl");

            if (files.IsNullOrEmpty())
            {
                ImGuiUtil.AddNotification(NotificationType.Error, $"No HSC playlists found.");
                return;
            }

            var playlistFile = files.First();

            PluginLog.Information($"HSC playlist path: '{playlistFile}'");

            HSC.DoLockedReadAction(() => PlaylistHelpers.OpenPlaylist(playlistFile, false));

            PluginLog.Information($"Load HSC playlist '{playlistFile}' success. total songs: {Settings.Playlist.Files.Count}");
        }

        private static void OpenPlaylistSettings()
        {
            string path = DalamudApi.api.PluginInterface.GetPluginConfigDirectory();

            var files = Directory.GetFiles(path, "*.settings.json");

            if (files.IsNullOrEmpty())
            {

                ImGuiUtil.AddNotification(NotificationType.Error, $"No HSC playlist settings found.'");
                return;
            }

            var settingsFile = files.First();


            PluginLog.Information($"HSC playlist settings path: '{settingsFile}'");

            HSC.DoLockedReadAction(() => PlaylistHelpers.LoadPlaylistSettings(settingsFile));

            PluginLog.Information($"Load HSC playlist '{settingsFile}' success. total songs: {Settings.Playlist.Files.Count}");
        }

        public static void ResetSettings(string songName, bool fromCurrent = false)
        {

            if (fromCurrent)
                songName = Path.GetFileNameWithoutExtension(Settings.Playlist.Files[Managers.PlaylistManager.CurrentPlaying]);

            if (Settings.PlaylistSettings.Settings.IsNullOrEmpty())
                return;

            if (!Settings.PlaylistSettings.Settings.ContainsKey(songName))
                return;

            var settings = Settings.PlaylistSettings.Settings[songName];
            settings.OctaveOffset = 0;
            settings.KeyOffset = 0;

            if (settings.Tracks.IsNullOrEmpty())
                return;

            foreach (var track in settings.Tracks.Values)
            {
                track.OctaveOffset = 0;
                track.KeyOffset = 0;
            }
        }

        public static void ApplySettings(bool fromCurrent = false)
        {
            try
            {
                if (Settings.CharIndex == -1)
                    return;

                if (fromCurrent)
                    Settings.AppSettings.CurrentSong = Path.GetFileNameWithoutExtension(Settings.Playlist.Files[Managers.PlaylistManager.CurrentPlaying]);

                Settings.CurrentSongSettings = Settings.PlaylistSettings.Settings[Settings.AppSettings.CurrentSong];

                Settings.OctaveOffset = Settings.CurrentSongSettings.OctaveOffset;
                Settings.KeyOffset = Settings.CurrentSongSettings.KeyOffset;

                if (!Settings.CurrentSongSettings.Tracks.IsNullOrEmpty())
                    UpdateTracks(Settings.CurrentSongSettings);
            }
            catch (Exception ex)
            {
                PluginLog.Error($"Applying HSC playlist settings for '{Settings.AppSettings.CurrentSong}' failed. Message: {ex.Message}");
            }
        }

        private static bool ShouldShiftTracks() => CurrentSongSettings.Tracks.Any(t => t.Value.TimeOffset != 0);

        public static void ReloadSettingsAndSwitch(bool loggedIn = false, bool loadSettings = true)
        {
            try
            {

                if (loadSettings)
                    HSC.DoLockedReadAction(() => {
                        Settings.Load();
                        CharConfigHelpers.UpdateCharIndex(Settings.CharName);
                    });

                if (Settings.CharIndex == -1)
                {
                    ImGuiUtil.AddNotification(NotificationType.Error, $"Reload playlist settings failed. Character config not loaded for '{Settings.CharName}'.");
                    return;
                }

                ImGuiUtil.AddNotification(NotificationType.Info, $"Reloading HSC playlist settings.");

                Settings.PlaylistSettings.Settings.Clear();

                OpenPlaylistSettings();

                if (Settings.PlaylistSettings == null || Settings.PlaylistSettings.Settings.IsNullOrEmpty())
                {
                    ImGuiUtil.AddNotification(NotificationType.Error, $"No HSC playlist settings are loaded.");
                    return;
                }

                if (string.IsNullOrEmpty(Settings.AppSettings.CurrentSong))
                {
                    ImGuiUtil.AddNotification(NotificationType.Error, $"No MIDI file chosen on HSC playlist.");
                    return;
                }

                //ImGuiUtil.AddNotification(NotificationType.Info, $"Reloading HSC playlist settings for '{Settings.AppSettings.CurrentSong}'.");

                if (!Settings.PlaylistSettings.Settings.ContainsKey(Settings.AppSettings.CurrentSong))
                {
                    //ImGuiUtil.AddNotification(NotificationType.Error, $"No HSC playlist settings loaded for '{Settings.AppSettings.CurrentSong}'.");
                    return;
                }

                wasPlaying = HSC.IsPlaying;

                //we only want the settings to change for the current song playing. dont apply settings for any other songs selected
                if (wasPlaying && Managers.PlaylistManager.CurrentPlaying != Settings.AppSettings.CurrentSongIndex) return;

                ApplySettings();


                bool switchInstruments = !loggedIn && !wasPlaying && Configuration.config.SwitchInstrumentFromPlaylist;

                if (wasPlaying)
                    Settings.PrevTime = HSC.CurrentPlayback.GetCurrentTime(Melanchall.DryWetMidi.Interaction.TimeSpanType.Metric);

                SwitchSongByName(Settings.AppSettings.CurrentSong, wasPlaying, switchInstruments);


                ImGuiUtil.AddNotification(NotificationType.Success, $"Reload HSC playlist settings for '{Settings.AppSettings.CurrentSong}' complete.");
            }

            catch (Exception e)
            {
                ImGuiUtil.AddNotification(NotificationType.Info, $"Reloading HSC playlist settings for '{Settings.AppSettings.CurrentSong}' failed.");
                PluginLog.Error(e, $"Reloading HSC playlist settings for '{Settings.AppSettings.CurrentSong}' failed. Message: {e.Message}");
            }
        }

        public static bool Reload(bool loggedIn = false, bool loadSettings = true)
        {
            try
            {
                if (loadSettings)
                    HSC.DoLockedReadAction(() => {
                        Settings.Load();
                        CharConfigHelpers.UpdateCharIndex(Settings.CharName);
                    });

                if (Settings.CharIndex == -1)
                {
                    ImGuiUtil.AddNotification(NotificationType.Error, $"Reload playlist failed. Character config not loaded for '{Settings.CharName}'.");
                    return false;
                }

                ImGuiUtil.AddNotification(NotificationType.Info, $"Reloading HSC playlist.");

                Settings.Playlist.Clear();

                try
                {
                    Managers.PlaylistManager.Clear();
                }
                catch (Exception ex)
                {
                    PluginLog.Error($"Clearing playlist failed. Message: {ex.Message}.");
                    return false;
                }

                wasPlaying = HSC.IsPlaying;

                OpenPlaylist();

                if (Settings.Playlist == null || Settings.Playlist.Files.IsNullOrEmpty())
                {
                    ImGuiUtil.AddNotification(NotificationType.Info, $"No songs in HSC playlist.");
                    Managers.PlaylistManager.CurrentPlaying = -1;
                    ClearTracks();
                    return false;
                }

                PluginLog.Information($"Updating MidiBard playlist.");
                Managers.PlaylistManager.Add(Settings.Playlist.Files.ToArray());
                PluginLog.Information($"Added {Settings.Playlist.Files.Count} files.");
                ImGuiUtil.AddNotification(NotificationType.Success, $"Added {Settings.Playlist.Files.Count} files.");

                if (Managers.PlaylistManager.CurrentPlaying != Settings.AppSettings.CurrentSongIndex && wasPlaying)
                    Managers.PlaylistManager.CurrentPlaying = Settings.AppSettings.CurrentSongIndex;

                return true;

            }

            catch (Exception e)
            {
                ImGuiUtil.AddNotification(NotificationType.Error, $"Reloading HSC playlist failed.");
                PluginLog.Error(e, $"Reloading HSC playlist failed. {e.Message}");
                return false;
            }
        }

        public static void ChangeSong(int index)
        {
            try
            {
                HSC.DoLockedReadAction(() =>
                {
                    Settings.Load();
                    CharConfigHelpers.UpdateCharIndex(Settings.CharName);
                });

                if (Settings.CharIndex == -1)
                {
                    ImGuiUtil.AddNotification(NotificationType.Error, $"Cannot change song from HSC. Character config not loaded for '{Settings.CharName}'.");
                    return;
                }

                if (Settings.Playlist == null || Settings.Playlist.Files.IsNullOrEmpty())
                {
                    ImGuiUtil.AddNotification(NotificationType.Error, $"Cannot change song from HSC. No songs on playlist.");
                    return;
                }

                wasPlaying = HSC.IsPlaying;

                if (wasPlaying && index != Managers.PlaylistManager.CurrentPlaying)
                {
                    ImGuiUtil.AddNotification(NotificationType.Error, "Cannot change songs from HSC playlist while playing.");
                    return;
                }

                Settings.PlaylistSettings.Settings.Clear();

                OpenPlaylistSettings();

                Settings.AppSettings.CurrentSong = Path.GetFileNameWithoutExtension(Settings.Playlist.Files[index]);
                Settings.AppSettings.CurrentSongIndex = index;

                PluginLog.Information($"Total playlist files '{Settings.Playlist.Files.Count}'.");

                try//file locking can throw error
                {
                    HSC.DoLockedWriteAction(() =>
                    {
                        Settings.Save();
                    });
                }
                catch (Exception ex)
                {
                    PluginLog.Information($"failed to save settings and configuration. Message: {ex.Message}.");
                }

                ImGuiUtil.AddNotification(NotificationType.Info, $"Changing to '{Settings.AppSettings.CurrentSong}' from HSC playlist.");

                if (!Settings.PlaylistSettings.Settings.ContainsKey(Settings.AppSettings.CurrentSong))
                {
                    ImGuiUtil.AddNotification(NotificationType.Error, $"No HSC playlist settings loaded for '{Settings.AppSettings.CurrentSong}'.");
                    return;
                }

                Settings.CurrentSongSettings = Settings.PlaylistSettings.Settings[Settings.AppSettings.CurrentSong];


                PluginLog.Information($"Total playlist settings saved '{Settings.PlaylistSettings.Settings.Count}'.");

                if (!Settings.CurrentSongSettings.Tracks.IsNullOrEmpty())
                    UpdateTracks(Settings.CurrentSongSettings);

                if (wasPlaying && index == Managers.PlaylistManager.CurrentPlaying)
                    return;

                SwitchSongByName(Settings.AppSettings.CurrentSong, Configuration.config.AutoPlaySong, Configuration.config.SwitchInstrumentFromPlaylist);
            }

            catch (Exception e)
            {
                PluginLog.Error(e, $"Changing song from HSC playlist failed. {e.Message}");
            }
        }

        public static void SwitchInstruments()
        {
            try
            {
                HSC.DoLockedReadAction(() =>
                {
                    Settings.Load();
                    CharConfigHelpers.UpdateCharIndex(Settings.CharName);
                });

                if (Settings.CharIndex == -1)
                {
                    ImGuiUtil.AddNotification(NotificationType.Error, $"Cannot switch instruments from HSC. Character config not loaded for '{Settings.CharName}'.");
                    return;
                }

                wasPlaying = HSC.IsPlaying;

                if (wasPlaying)
                {
                    ImGuiUtil.AddNotification(NotificationType.Error, "Cannot switch instruments from HSC playlist while playing.");
                    return;
                }

                if (string.IsNullOrEmpty(Settings.AppSettings.CurrentSong))
                {
                    ImGuiUtil.AddNotification(NotificationType.Error, $"No MIDI file chosen on HSC playlist.");
                    return;
                }

                if (Settings.PlaylistSettings == null || Settings.PlaylistSettings.Settings.IsNullOrEmpty())
                {
                    ImGuiUtil.AddNotification(NotificationType.Error, $"No HSC playlist settings are loaded.");
                    return;
                }

                if (!Settings.PlaylistSettings.Settings.ContainsKey(Settings.AppSettings.CurrentSong))
                {
                    ImGuiUtil.AddNotification(NotificationType.Error, $"No HSC playlist settings loaded for '{Settings.AppSettings.CurrentSong}'.");
                    return;
                }

                Settings.CurrentSongSettings = Settings.PlaylistSettings.Settings[Settings.AppSettings.CurrentSong];

                PerformanceHelpers.SwitchInstrumentFromSong(true);
            }

            catch (Exception e)
            {
                PluginLog.Error(e, $"Changing song from HSC playlist failed. {e.Message}");
            }
        }

        private static void SwitchSongByName(string name, bool startPlaying = false, bool switchInstrument = true)
        {

            var song = Managers.PlaylistManager.GetSongByName(name);

            if (song == null)
            {
                PluginLog.Error($"Error: song does not exist on playlist '{name}'.");
                return;
            }

            Control.MidiControl.MidiPlayerControl.SwitchSong(song.Value.index, startPlaying, switchInstrument);
        }
    }

}
