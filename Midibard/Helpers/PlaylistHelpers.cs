using HSC.Common;

using HSC.Models.Music;
using HSC.Models.Settings;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HSC.Helpers
{
    /// <summary>
    /// author:  SpuriousSnail86
    /// </summary>
    public class PlaylistHelpers
    {
        private const string PlaylistDefaultFileName = "playlist.pl";

        private static string GetDefaultPlaylistFilePath()
        {
            return Path.Combine($"Playlists", PlaylistDefaultFileName);
        }

        public static void Clear()
        {
            Settings.Playlist.Clear();
            Settings.PlaylistSettings.Clear();
        }


        public static void LoadPlaylistSettings(string settingsFile)
        {
            if (string.IsNullOrEmpty(settingsFile))
                return;

            var playlistSettings = FileHelpers.Load<Models.Settings.SongSettings>(settingsFile);

            if (playlistSettings != null)
                Settings.PlaylistSettings = playlistSettings;
        }

        public static void LoadSongSettings(string filePath)
        {
            try
            {
                var settings = FileHelpers.Load<MidiSequence>(filePath);

                if (settings == null)
                    return;

                //ignore index
                //var plSettings = Common.Settings.PlaylistSettings.Settings[settings.Info.Title];
                //settings.Index = plSettings.Index;
            }
            catch (Exception ex)
            {
                /*AppendLog("", $"Error: unable to load MIDI settings '{filePath}'.")*/

            }
        }

        public static void OpenPlaylist(string playlistFilePath, bool loadSettings = true)
        {
            Settings.Playlist.Title = Path.GetFileNameWithoutExtension(playlistFilePath);

            if (!File.Exists(playlistFilePath))
                return;

            var playlist = FileHelpers.Load<Models.Playlist.Playlist>(playlistFilePath);

            if (playlist == null || playlist.IsEmpty)
                return;

           Settings.Playlist = playlist;

            if (loadSettings)
                LoadPlaylistSettings(playlist.SettingsFile);
        }

    }
}
