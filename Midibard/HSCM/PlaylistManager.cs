﻿using Dalamud.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MidiBard.Common;
using MidiBard.HSC.Models;
using MidiBard.Control.MidiControl;
using MidiBard.Control.CharacterControl;
using System.Threading;
using MidiBard.HSC.Music;
using static MidiBard.HSC.Settings;
using MidiBard.HSC;
using Dalamud.Interface.Internal.Notifications;

namespace MidiBard.HSCM
{
    /// <summary>
    /// this will sync HSC playlist with midibard one
    /// </summary>
    public class PlaylistManager
    {
        private static bool wasPlaying;

        private static void UpdatePercussionNote(int trackIndex, int note)
        {
            if (!HSC.Settings.PercussionNotes.ContainsKey(trackIndex))
                HSC.Settings.PercussionNotes[trackIndex] = new Dictionary<int, bool>() { { note, true } };
            else
                HSC.Settings.PercussionNotes[trackIndex].Add(note, true);
        }


        private static void UpdateMappedTracks(int parentIndex, TrackTransposeInfo info)
        {
            if (!HSC.Settings.MappedTracks.ContainsKey(parentIndex))
                HSC.Settings.MappedTracks.Add(parentIndex, info);
        }

        private static void ClearTracks()
        {
            if (ConfigurationPrivate.config.EnabledTracks.IsNullOrEmpty())
                return;

            int total = ConfigurationPrivate.config.EnabledTracks.Length;
            for (int i = 0; i < total; i++)
            {
                ConfigurationPrivate.config.EnabledTracks[i] = false;
            }

            if (!MidiBard.CurrentTracks.IsNullOrEmpty())
                MidiBard.CurrentTracks.Clear();

            MidiBard.DoMutexAction(() => Configuration.Save());
        }

        private static void UpdateTracks(MidiSequence seq)
        {
            if (ConfigurationPrivate.config.EnabledTracks.IsNullOrEmpty() || seq.Tracks.IsNullOrEmpty())
                return;

            PluginLog.Information($"Updating tracks of '{Configuration.config.hscmMidiFile}' from HSCM playlist.");

            int index = 0;

            HSC.Settings.PercussionNotes = new Dictionary<int, Dictionary<int, bool>>();
            HSC.Settings.MappedTracks = new Dictionary<int, TrackTransposeInfo>();
            HSC.Settings.TrackInfo = new Dictionary<int, TrackTransposeInfo>();


            foreach (var track in seq.Tracks)
            {
                var info = new TrackTransposeInfo() { KeyOffset = track.Value.KeyOffset, OctaveOffset = track.Value.OctaveOffset };

                if (!HSC.Settings.TrackInfo.ContainsKey(index))
                    HSC.Settings.TrackInfo.Add(index, info);

                if (Configuration.config.OverrideGuitarTones && PerformHelpers.HasGuitar(track.Value))
                    Configuration.config.TonesPerTrack[index] = PerformHelpers.GetGuitarTone(track.Value);

                if (!track.Value.Muted && track.Value.EnsembleMember == HSC.Settings.CharIndex)
                {
                    if (track.Value.PercussionNote.HasValue && track.Value.ParentIndex.HasValue)
                    {
                        PluginLog.Information($"Percussion track {index} ({track.Value.PercussionNote.Value}) has parent {track.Value.ParentIndex} from HSCM playlist");
                        UpdatePercussionNote(track.Value.ParentIndex.Value, track.Value.PercussionNote.Value);
                    }

                    PluginLog.Information($"Track {index} is assigned from HSCM playlist");

                    //percussion + duplication logic. if track has parent enable its parent
                    if (track.Value.ParentIndex.HasValue)
                    {
                        ConfigurationPrivate.config.EnabledTracks[track.Value.ParentIndex.Value] = true;

                        UpdateMappedTracks(track.Value.ParentIndex.Value, info);
                    }
                    else//no parent enable as normal
                        ConfigurationPrivate.config.EnabledTracks[index] = true;
                }
                else
                    ConfigurationPrivate.config.EnabledTracks[index] = false;
                index++;
            }

            MidiBard.DoMutexAction(() => Configuration.Save());
        }

        private static void OpenPlaylist()
        {
            string path = HSC.Settings.CurrentAppPath;

            var files = Directory.GetFiles(path, "*.pl");

            if (files.IsNullOrEmpty())
            {

                PluginLog.Information($"No HSCM playlists found.'");
                return;
            }

            var playlistFile = files.First();

            PluginLog.Information($"HSCM playlist path: '{playlistFile}'");

            MidiBard.DoMutexAction(() => HSC.Playlist.Playlist.OpenPlaylist(playlistFile, false));

            PluginLog.Information($"Load HSCM playlist '{playlistFile}' success. total songs: {HSC.Settings.Playlist.Files.Count}");
        }

        private static void OpenPlaylistSettings()
        {
            string path = HSC.Settings.CurrentAppPath;

            var files = Directory.GetFiles(path, "*.json");

            if (files.IsNullOrEmpty())
            {

                PluginLog.Information($"No HSCM playlist settings found.'");
                return;
            }

            var settingsFile = files.First();


            PluginLog.Information($"HSCM playlist settings path: '{settingsFile}'");

            MidiBard.DoMutexAction(() => HSC.Playlist.Playlist.LoadPlaylistSettings(settingsFile));

            PluginLog.Information($"Load HSCM playlist '{settingsFile}' success. total songs: {HSC.Settings.Playlist.Files.Count}");
        }

        public static void ApplySettings(bool fromCurrent = false)
        {
            try
            {
                if (fromCurrent)
                    Settings.AppSettings.CurrentSong = Path.GetFileNameWithoutExtension(HSC.Settings.Playlist.Files[Managers.PlaylistManager.CurrentPlaying]);

                HSC.Settings.CurrentSongSettings = HSC.Settings.PlaylistSettings.Settings[Settings.AppSettings.CurrentSong];

                HSC.Settings.OctaveOffset = HSC.Settings.CurrentSongSettings.OctaveOffset;
                HSC.Settings.KeyOffset = HSC.Settings.CurrentSongSettings.KeyOffset;

                if (!HSC.Settings.CurrentSongSettings.Tracks.IsNullOrEmpty())
                    UpdateTracks(HSC.Settings.CurrentSongSettings);
            }
            catch (Exception ex)
            {
                PluginLog.Error($"Applying HSCM playlist settings for '{Settings.AppSettings.CurrentSong}' failed. Message: {ex.Message}");
            }
        }

        public static void ReloadSettingsAndSwitch(bool loggedIn = false)
        {
            try
            {
                if (string.IsNullOrEmpty(Settings.AppSettings.CurrentSong))
                {
                    ImGuiUtil.AddNotification(NotificationType.Error, $"No MIDI file chosen on HSCM playlist.");
                    return;
                }

                ImGuiUtil.AddNotification(NotificationType.Info, $"Reloading HSCM playlist settings for '{Settings.AppSettings.CurrentSong}'.");

                wasPlaying = MidiBard.IsPlaying;

                HSC.Settings.PlaylistSettings.Settings.Clear();

                MidiBard.DoMutexAction(() => OpenPlaylistSettings());

                if (HSC.Settings.PlaylistSettings == null || HSC.Settings.PlaylistSettings.Settings.IsNullOrEmpty())
                {
                    ImGuiUtil.AddNotification(NotificationType.Error, $"No HSCM playlist settings are loaded.");
                    return;
                }

                if (!HSC.Settings.PlaylistSettings.Settings.ContainsKey(Settings.AppSettings.CurrentSong))
                {
                    ImGuiUtil.AddNotification(NotificationType.Error, $"No HSCM playlist settings loaded for '{Settings.AppSettings.CurrentSong}'.");
                    return;
                }

                ApplySettings();

                if (wasPlaying)
                    HSC.Settings.PrevTime = MidiBard.CurrentPlayback.GetCurrentTime(Melanchall.DryWetMidi.Interaction.TimeSpanType.Metric);

                bool switchInstruments = !loggedIn && !wasPlaying && Configuration.config.switchInstrumentFromHscmPlaylist;

                SwitchSongByName(Settings.AppSettings.CurrentSong, wasPlaying, switchInstruments);

                if (!loggedIn && Configuration.config.hscmShowUI)
                    MidiBard.Ui.Open();

                ImGuiUtil.AddNotification(NotificationType.Success, $"Reload HSCM playlist settings for '{Settings.AppSettings.CurrentSong}' complete.");
            }

            catch (Exception e)
            {
                ImGuiUtil.AddNotification(NotificationType.Info, $"Reloading HSCM playlist settings for '{Settings.AppSettings.CurrentSong}' failed.");
                PluginLog.Error(e, $"Reloading HSCM playlist settings for '{Settings.AppSettings.CurrentSong}' failed. Message: {e.Message}");
            }
        }

        public static void Reload(bool loggedIn = false)
        {
            ImGuiUtil.AddNotification(NotificationType.Info, $"Reloading HSCM playlist.");

            try
            {
                HSC.Settings.Playlist.Clear();
                try
                {
                    Managers.PlaylistManager.Clear();
                }
                catch { }


                MidiBard.DoMutexAction(() => OpenPlaylist());

                if (HSC.Settings.Playlist == null || HSC.Settings.Playlist.Files.IsNullOrEmpty())
                {
                    ImGuiUtil.AddNotification(NotificationType.Info, $"No songs in HSCM playlist.");
                    Managers.PlaylistManager.CurrentPlaying = -1;
                    Managers.PlaylistManager.CurrentSelected = -1;
                    ClearTracks();
                    return;
                }

                PluginLog.Information($"Updating MidiBard playlist.");
                MidiBardPlaylistManager.Add(HSC.Settings.Playlist.Files.ToArray());
                PluginLog.Information($"Added {HSC.Settings.Playlist.Files.Count} files.");
                ImGuiUtil.AddNotification(NotificationType.Success, $"Added {HSC.Settings.Playlist.Files.Count} files.");

                if (!loggedIn && Configuration.config.hscmShowUI)
                    MidiBard.Ui.Open();
  
                if (Managers.PlaylistManager.CurrentSelected != Settings.AppSettings.CurrentSongIndex)
                    Managers.PlaylistManager.CurrentSelected = Settings.AppSettings.CurrentSongIndex;

                if (Managers.PlaylistManager.CurrentPlaying != Settings.AppSettings.CurrentSongIndex)
                    Managers.PlaylistManager.CurrentPlaying = Settings.AppSettings.CurrentSongIndex;

            }

            catch (Exception e)
            {
                ImGuiUtil.AddNotification(NotificationType.Error, $"Reloading HSCM playlist failed.");
                PluginLog.Error(e, $"Reloading HSCM playlist failed. {e.Message}");
            }
        }

        public static void ChangeSong(int index)
        {
            try
            {
                if (HSC.Settings.Playlist == null || HSC.Settings.Playlist.Files.IsNullOrEmpty())
                {
                    ImGuiUtil.AddNotification(NotificationType.Error, $"Cannot change song from HSCM. No songs on playlist.");
                    return;
                }

                wasPlaying = MidiBard.IsPlaying;

                if (wasPlaying)
                {
                    ImGuiUtil.AddNotification(NotificationType.Error, "Cannot change songs from HSCM playlist while playing.");
                    return;
                }

                HSC.Settings.PlaylistSettings.Settings.Clear();

                MidiBard.DoMutexAction(() => OpenPlaylistSettings());

                Settings.AppSettings.CurrentSong = Path.GetFileNameWithoutExtension(HSC.Settings.Playlist.Files[index]);
                Settings.AppSettings.CurrentSongIndex = index;


                PluginLog.Information($"TOTAL {HSC.Settings.Playlist.Files.Count}");

                try//file locking can throw error
                {
                    MidiBard.DoMutexAction(() => {
                        Settings.Save();
                        Configuration.Save();
                    });
                }
                catch { }

                ImGuiUtil.AddNotification(NotificationType.Info, $"Changing to '{Settings.AppSettings.CurrentSong}' from HSCM playlist.");

                if (!HSC.Settings.PlaylistSettings.Settings.ContainsKey(Settings.AppSettings.CurrentSong))
                {
                    ImGuiUtil.AddNotification(NotificationType.Error, $"No HSCM playlist settings loaded for '{Settings.AppSettings.CurrentSong}'.");
                    return;
                }

                HSC.Settings.CurrentSongSettings = HSC.Settings.PlaylistSettings.Settings[Settings.AppSettings.CurrentSong];


                PluginLog.Information($"TOTAL2 {HSC.Settings.PlaylistSettings.Settings.Count}");

                if (!HSC.Settings.CurrentSongSettings.Tracks.IsNullOrEmpty())
                    UpdateTracks(HSC.Settings.CurrentSongSettings);

                if (Configuration.config.hscmShowUI)
                    MidiBard.Ui.Open();

                SwitchSongByName(Settings.AppSettings.CurrentSong, Configuration.config.hscmAutoPlaySong, true);
            }

            catch (Exception e)
            {
                PluginLog.Error(e, $"Changing song from HSCM playlist failed. {e.Message}");
            }
        }

        public static void SwitchInstruments()
        {
            try
            {
                wasPlaying = MidiBard.IsPlaying;

                if (wasPlaying)
                {
                    ImGuiUtil.AddNotification(NotificationType.Error, "Cannot switch instruments from HSCM playlist while playing.");
                    return;
                }

                if (string.IsNullOrEmpty(Settings.AppSettings.CurrentSong))
                {
                    ImGuiUtil.AddNotification(NotificationType.Error, $"No MIDI file chosen on HSCM playlist.");
                    return;
                }

                if (HSC.Settings.PlaylistSettings == null || HSC.Settings.PlaylistSettings.Settings.IsNullOrEmpty())
                {
                    ImGuiUtil.AddNotification(NotificationType.Error, $"No HSCM playlist settings are loaded.");
                    return;
                }

                if (!HSC.Settings.PlaylistSettings.Settings.ContainsKey(Settings.AppSettings.CurrentSong))
                {
                    ImGuiUtil.AddNotification(NotificationType.Error, $"No HSCM playlist settings loaded for '{Settings.AppSettings.CurrentSong}'.");
                    return;
                }

                HSC.Settings.CurrentSongSettings = HSC.Settings.PlaylistSettings.Settings[Settings.AppSettings.CurrentSong];

                PerformHelpers.SwitchInstrumentFromSong(true);

                if (Configuration.config.hscmShowUI)
                    MidiBard.Ui.Open();
            }

            catch (Exception e)
            {
                PluginLog.Error(e, $"Changing song from HSCM playlist failed. {e.Message}");
            }
        }

        private static void SwitchSongByName(string name, bool startPlaying = false, bool switchInstrument = true)
        {

            var song = MidiBardPlaylistManager.GetSongByName(name);

            if (song == null)
            {
                PluginLog.Error($"Error: song does not exist on playlist '{name}'.");
                return;
            }

            Control.MidiControl.MidiPlayerControl.SwitchSong(song.Value.index, startPlaying, switchInstrument);
        }
    }

}