using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HSC.Common;
using HSC.Control.MidiControl.PlaybackInstance;
using Dalamud.Logging;
using System.Diagnostics;
using HSC.Models.Music;
using HSC.Models.Settings;
using HSC.Config;

namespace HSC.Midi
{
    /// <summary>
    /// author:  SpuriousSnail86
    /// </summary>
    internal class MidiProcessor
    {
        public static TimedEventWithTrackChunkIndex[] Process(TimedEventWithTrackChunkIndex[] timedObjs, MidiSequence settings)
        {
            PluginLog.Information($"HSC processing song '{Settings.AppSettings.CurrentSong}', {timedObjs.Count()} events before processing.");

            var stopwatch = Stopwatch.StartNew();

            var tracks = timedObjs.GroupBy(to => (int)to.Metadata)
                .Select(to => new { index = to.Key, track = to.ToArray().ToTrackChunk() })
                .AsParallel().Where(t => t.track.GetNotes().Any())
                .OrderBy(t => t.index).Select((t, i) => new { t.track, index = i }).ToDictionary(t => t.index, t => t.track);

            if (Configuration.config.UseChordTrimming)
                ChordTrimmer.Trim(tracks, settings, 2, false, Configuration.config.UseTrimByTrack);

            ProcessTracks(tracks, settings);

            var newEvs = tracks.Values
          .SelectMany((chunk, index) => chunk.GetTimedEvents().Select(e => new TimedEventWithTrackChunkIndex(e.Event, e.Time, index))).ToArray();

            PluginLog.Information($"HSC process of '{Settings.AppSettings.CurrentSong}' finished in {stopwatch.Elapsed.TotalMilliseconds}, {newEvs.Count()} events after processing.");

            return newEvs;

        }

        public static void Process(MidiFile midiFile, MidiSequence settings)
        {
            PluginLog.Information($"HSC processing song '{Settings.AppSettings.CurrentSong}', {midiFile.GetTimedEvents().Count()} events before processing.");

            var stopwatch = Stopwatch.StartNew();

            var tracks = midiFile.GetTrackChunks()
               .Select((t, i) => new { track = t, index = i })
               .AsParallel().Where(t => t.track.GetNotes().Any())
            .OrderBy(t => t.index).Select((t, i) => new { t.track, index = i }).ToDictionary(t => t.index, t => t.track);

            if (Configuration.config.UseChordTrimming)
                ChordTrimmer.Trim(tracks, settings, 2, false, Configuration.config.UseTrimByTrack);

            ProcessTracks(tracks, settings);

            PluginLog.Information($"HSC process of '{Settings.AppSettings.CurrentSong}' finished in {stopwatch.Elapsed.TotalMilliseconds}, {tracks.Select(t => t.Value).GetTimedEvents().Count()} events after processing.");
        }

        private static void ProcessTracks(Dictionary<int, TrackChunk> tracks, MidiSequence settings)
        {
            Parallel.ForEach(tracks, t =>
            {
                if (settings.Tracks.ContainsKey(t.Key))
                {
                    var trackSettings = settings.Tracks[t.Key];

                    t.Value.ProcessNotes(n => ProcessNote(n, settings, trackSettings, t.Key));
                    t.Value.RemoveNotes(n => !ShouldPlayDrumNote(n.NoteNumber, t.Key)); //remove the drum notes this person should not play
                }
            });
        }

        private static void ProcessNote(Note note, MidiSequence settings, Track trackSettings, int trackIndex)
        {
            try
            {
                if (trackSettings.TimeOffset != 0 && note.Time >= Math.Abs(trackSettings.TimeOffset))
                    ShiftTime(note, trackSettings.TimeOffset);

                if (Configuration.config.UseTransposing)
                    Transpose(note, settings, trackIndex);
            }
            catch(Exception ex)
            {
                PluginLog.Error(ex, "error processing note");
            }
        }

        private static void ShiftTime(Note note, int offset) => note.Time += offset;

        private static void Transpose(Note note, MidiSequence settings, int trackindex)
        {
            int newNote = 0;
            int oldNote = (int)note.NoteNumber;

            newNote = GetTransposedNoteNum(oldNote, settings, trackindex);

            //PluginLog.Information($"old value: {oldNote}, new value: {newNote}");

            note.NoteNumber = new SevenBitNumber((byte)newNote);

        }

        private static int GetTransposedValue(int trackIndex, MidiSequence settings)
        {
            int noteNum = 0;

            var trackInfo = Settings.TrackInfo[trackIndex];

            if (trackInfo == null)
                return 0;

            if (trackInfo.OctaveOffset != 0)
                noteNum += 12 * trackInfo.OctaveOffset;

            if (trackInfo.KeyOffset != 0)
                noteNum += trackInfo.KeyOffset;

            return (12 * settings.OctaveOffset) + settings.KeyOffset + noteNum;
        }


        private static int GetTransposedNoteNum(int noteNumber, MidiSequence settings, int trackIndex) => noteNumber + GetTransposedValue(trackIndex, settings);

        private static Settings.TrackTransposeInfo GetHSCTrackInfo(int trackIndex)
        {
            if (Settings.MappedTracks.ContainsKey(trackIndex))
                return Settings.MappedTracks[trackIndex];

            if (!Settings.TrackInfo.ContainsKey(trackIndex))
                return null;

            return Settings.TrackInfo[trackIndex];
        }

        private static bool ShouldPlayDrumNote(int noteNum, int trackIndex)
        {
            if (Settings.PercussionNotes.IsNullOrEmpty())
                return true;

            //not a percussion note so play anyway
            if (!Settings.PercussionNotes.ContainsKey(trackIndex))
                return true;

            //percussion note - do percussion logic
            return Settings.PercussionNotes[trackIndex].ContainsKey(noteNum) && Settings.PercussionNotes[trackIndex][noteNum];
        }

    }
}
