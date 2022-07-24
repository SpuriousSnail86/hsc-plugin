using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Logging;
using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Multimedia;
using Melanchall.DryWetMidi.Interaction;
using Melanchall.DryWetMidi.Standards;
using Melanchall.DryWetMidi.Tools;
using HSC.Control.CharacterControl;
using HSC.Control.MidiControl.PlaybackInstance;
using HSC.Managers.Ipc;
using HSC.Util;
using static HSC.HSC;
using System.IO;
using HSC.Common;
using HSC.Managers;
using HSC.Config;
using HSC.Helpers;
using HSC.Midi;
using HSC.Models.Settings;
using HSC.IPC;
using static HSC.Control.MidiControl.MidiPlayerControl;

namespace HSC.Control.MidiControl;

/// <summary>
/// author: akira045/Ori, modified by SpuriousSnail86
/// </summary>
public static class FilePlayback
{
    private static readonly Regex regex = new Regex(@"^#.*?([-|+][0-9]+).*?#", RegexOptions.Compiled | RegexOptions.IgnoreCase);



    public static (BardPlayback playback, TimedEventWithTrackChunkIndex[] timedEvs) GetPlayback(TimedEventWithTrackChunkIndex[] timedEvs, TempoMap tempoMap, string trackName)
    {
        PluginLog.Information($"[LoadPlayback] -> {trackName} START");
        var stopwatch = Stopwatch.StartNew();

        try
        {
            HSC.CurrentTMap = tempoMap;
        }
        catch (Exception e)
        {
            PluginLog.Warning("[LoadPlayback] error when getting file TempoMap, using default TempoMap instead.");
            HSC.CurrentTMap = TempoMap.Default;
        }
        PluginLog.Information($"[LoadPlayback] -> {trackName} 1 in {stopwatch.Elapsed.TotalMilliseconds} ms");
        try
        {
            HSC.CurrentTracks =

                timedEvs.GroupBy(to => (int)to.Metadata)
                    .Select(to => new { index = to.Key, track = TimedObjectUtilities.ToTrackChunk(to.ToArray()) })
                     .Where(c => c.track.GetNotes().Any())
                    .OrderBy(t => t.index)
                    .ToDictionary(t => t.index, t => t.track);
        }
        catch (Exception exception1)
        {
            PluginLog.Warning(exception1, $"[LoadPlayback] error when parsing tracks, falling back to generated NoteEvent playback.");

            try
            {

                HSC.CurrentTracks =
                timedEvs.GroupBy(to => (int)to.Metadata)
                    .Select(to => new { index = to.Key, track = TimedObjectUtilities.ToTrackChunk(to.ToArray()) })
                     .Where(c => c.track.GetNotes().Any())
                    .OrderBy(t => t.index)
                    .ToDictionary(t => t.index, t => t.track);
            }
            catch (Exception exception2)
            {
                PluginLog.Error(exception2, "[LoadPlayback] still errors? check your file");
                throw;
            }
        }
        PluginLog.Information($"[LoadPlayback] -> {trackName} 2 in {stopwatch.Elapsed.TotalMilliseconds} ms");
        //int givenIndex = 0;
        //CurrentTracks.ForEach(tuple => tuple.trackInfo.Index = givenIndex++);

        var timedEvents = HSC.CurrentTracks.Select(t => t.Value).AsParallel()
            .SelectMany((chunk, index) => chunk.GetTimedEvents().Select(e =>
            {
                var compareValue = e.Event switch
                {
                        //order chords so they always play from low to high
                    NoteOnEvent noteOn => noteOn.NoteNumber,
                        //order program change events so they always get processed before notes 
                    ProgramChangeEvent => -2,
                        //keep other unimportant events order
                    _ => -1
                };
                return (compareValue, timedEvent: new TimedEventWithTrackChunkIndex(e.Event, e.Time, index));
            }))
            .OrderBy(e => e.timedEvent.Time)
            .ThenBy(i => i.compareValue)
            .Select(i => i.timedEvent).ToArray(); //this is crucial as have executed a parallel query 

        PluginLog.Information($"[LoadPlayback] -> {trackName} 3 in {stopwatch.Elapsed.TotalMilliseconds} ms");
        //var (programTrackChunk, programTrackInfo) =
        //    CurrentTracks.FirstOrDefault(i => Regex.IsMatch(i.trackInfo.TrackName, @"^Program:.+$", RegexOptions.Compiled | RegexOptions.IgnoreCase));

        Array.Fill(HSC.CurrentOutputDevice.Channels, new PlaybackDevice.ChannelState());

        PluginLog.Information($"[LoadPlayback] -> {trackName} 3.1 in {stopwatch.Elapsed.TotalMilliseconds} ms");
        var playback = new BardPlayback(timedEvents, HSC.CurrentTMap, new MidiClockSettings { CreateTickGeneratorCallback = () => new HighPrecisionTickGenerator() })
        {
            InterruptNotesOnStop = true,
            Speed = Configuration.config.PlaySpeed,
            TrackProgram = true
        };

        PluginLog.Information($"[LoadPlayback] -> {trackName} 4 in {stopwatch.Elapsed.TotalMilliseconds} ms");
        PluginLog.Information($"[LoadPlayback] Channels for {trackName}:");
        for (int i = 0; i < HSC.CurrentOutputDevice.Channels.Length; i++)
        {
            uint prog = HSC.CurrentOutputDevice.Channels[i].Program;
            PluginLog.Information($"  - [{i}]: {Util.ProgramNames.GetGMProgramName((byte)prog)} ({prog})");
        }

        PluginLog.Information($"[LoadPlayback] -> {trackName} OK! in {stopwatch.Elapsed.TotalMilliseconds} ms");

        playback.Started += CurrentPlayback_Started;
        playback.Stopped += CurrentPlayback_Stopped;
        playback.Finished += CurrentPlayback_Finished;


        return (playback, timedEvs);
    }


    public static BardPlayback GetFilePlayback(MidiFile midifile, string trackName)
    {
        PluginLog.Information($"[LoadPlayback] -> {trackName} START");
        var stopwatch = Stopwatch.StartNew();

        try
        {
            CurrentTMap = midifile.GetTempoMap();
        }
        catch (Exception e)
        {
            PluginLog.Warning("[LoadPlayback] error when getting file TempoMap, using default TempoMap instead.");
            CurrentTMap = TempoMap.Default;
        }
        PluginLog.Information($"[LoadPlayback] -> {trackName} 1 in {stopwatch.Elapsed.TotalMilliseconds} ms");
        try
        {
            CurrentTracks = midifile.GetTrackChunks()
                .Select((t, i) => new { track = t, index = i})
                .Where(t => t.track.Events.Any(ev => ev is NoteOnEvent))
                .OrderBy(t => t.index)
                    .ToDictionary(t => t.index, t => t.track);
        }
        catch (Exception exception1)
        {
            PluginLog.Warning(exception1, $"[LoadPlayback] error when parsing tracks, falling back to generated NoteEvent playback.");

            try
            {
                PluginLog.Debug($"[LoadPlayback] file.Chunks.Count {midifile.Chunks.Count}");
                var trackChunks = midifile.GetTrackChunks().ToList();
                PluginLog.Debug($"[LoadPlayback] file.GetTrackChunks.Count {trackChunks.Count}");
                PluginLog.Debug($"[LoadPlayback] file.GetTrackChunks.First {trackChunks.First()}");
                PluginLog.Debug($"[LoadPlayback] file.GetTrackChunks.Events.Count {trackChunks.First().Events.Count}");
                PluginLog.Debug($"[LoadPlayback] file.GetTrackChunks.Events.OfType<NoteEvent>.Count {trackChunks.First().Events.OfType<NoteEvent>().Count()}");


                CurrentTracks = midifile.GetTrackChunks()
                    .Select((t, i) => new { track = t, index = i })
                    .Where(t => t.track.Events.Any(ev => ev is NoteOnEvent))
                    .OrderBy(t => t.index)
                    .ToDictionary(t => t.index, t => t.track);
            }
            catch (Exception exception2)
            {
                PluginLog.Error(exception2, "[LoadPlayback] still errors? check your file");
                throw;
            }
        }
        PluginLog.Information($"[LoadPlayback] -> {trackName} 2 in {stopwatch.Elapsed.TotalMilliseconds} ms");
        //int givenIndex = 0;
        //CurrentTracks.ForEach(tuple => tuple.trackInfo.Index = givenIndex++);

        var timedEvents = CurrentTracks.Select(t => t.Value).AsParallel()
            .SelectMany((chunk, index) => chunk.GetTimedEvents().Select(e =>
            {
                var compareValue = e.Event switch
                {
                    //order chords so they always play from low to high
                    NoteOnEvent noteOn => noteOn.NoteNumber,
                    //order program change events so they always get processed before notes 
                    ProgramChangeEvent => -2,
                    //keep other unimportant events order
                    _ => -1
                };
                return (compareValue, timedEvent: new TimedEventWithTrackChunkIndex(e.Event, e.Time, index));
            }))
            .OrderBy(e => e.timedEvent.Time)
            .ThenBy(i => i.compareValue)
            .Select(i => i.timedEvent).ToArray(); //this is crucial as have executed a parallel query 

        PluginLog.Information($"[LoadPlayback] -> {trackName} 3 in {stopwatch.Elapsed.TotalMilliseconds} ms");

        Array.Fill(CurrentOutputDevice.Channels, new PlaybackDevice.ChannelState());

        PluginLog.Information($"[LoadPlayback] -> {trackName} 3.1 in {stopwatch.Elapsed.TotalMilliseconds} ms");
        var playback = new BardPlayback(timedEvents, CurrentTMap, new MidiClockSettings { CreateTickGeneratorCallback = () => new HighPrecisionTickGenerator() })
        {
            InterruptNotesOnStop = true,
            Speed = Configuration.config.PlaySpeed,
            TrackProgram = true,
#if DEBUG
            NoteCallback = (data, time, length, playbackTime) =>
            {
                PluginLog.Verbose($"[NOTE] {new Note(data.NoteNumber)} time:{time} len:{length} time:{playbackTime}");
                return data;
            }
#endif
        };
        PluginLog.Information($"[LoadPlayback] -> {trackName} 4 in {stopwatch.Elapsed.TotalMilliseconds} ms");
        PluginLog.Information($"[LoadPlayback] Channels for {trackName}:");
        for (int i = 0; i < CurrentOutputDevice.Channels.Length; i++)
        {
            uint prog = CurrentOutputDevice.Channels[i].Program;
            PluginLog.Information($"  - [{i}]: {ProgramNames.GetGMProgramName((byte)prog)} ({prog})");
        }

        PluginLog.Information($"[LoadPlayback] -> {trackName} OK! in {stopwatch.Elapsed.TotalMilliseconds} ms");


        playback.Started += CurrentPlayback_Started;
        playback.Stopped += CurrentPlayback_Stopped;
        playback.Finished += CurrentPlayback_Finished;

        return playback;
    }


    public static DateTime? waitUntil { get; set; } = null;
    public static DateTime? waitStart { get; set; } = null;
    public static bool isWaiting => waitUntil != null && DateTime.Now < waitUntil;

    public static float waitProgress
    {
        get
        {
            float valueTotalMilliseconds = 1;
            if (isWaiting)
            {
                try
                {
                    if (waitUntil != null)
                        if (waitStart != null)
                            valueTotalMilliseconds = 1 -
                                                     (float)((waitUntil - DateTime.Now).Value.TotalMilliseconds /
                                                             (waitUntil - waitStart).Value.TotalMilliseconds);
                }
                catch (Exception e)
                {
                    PluginLog.Error(e, "error when get current wait progress");
                }
            }

            return valueTotalMilliseconds;
        }
    }

    //cached playback - dont load midi file if already stored for huge performance improvements doing adjustments e.g chord trimming
    //this should be included in MidiBard_LoadPlayback in the future as its very beneficial 
    public static bool LoadPlayback(int index, bool startPlaying = false, bool switchInstrument = true)
    {
        IpcManager.LoadStarted(index);

        var wasPlaying = IsPlaying;

        CurrentPlayback?.Dispose();
        CurrentPlayback = null;

        string songName = Managers.PlaylistManager.FilePathList[index].displayName;

        if (!Configuration.config.UseSongCache)//not using cache - always load midi file
        {
            var midiFile = Managers.PlaylistManager.LoadMidiFile(index);

            if (midiFile == null)
            {
                // delete file if can't be loaded(likely to be deleted locally)
                PluginLog.Debug($"[LoadPlayback] removing {index}");
                //PluginLog.Debug($"[LoadPlayback] removing {PlaylistManager.FilePathList[index].path}");
                Managers.PlaylistManager.FilePathList.RemoveAt(index);
                return false;
            }

            IPC.PlaylistManager.ApplySettings(true);//this should allow the HSC playlist to be looped 

            CurrentPlayback = global::HSC.Midi.PlaybackUtilities.GetProcessedMidiPlayback(midiFile, songName);
        }
        else// using cache
        {
            if (!SongCache.IsCached(songName))//song not cached? load midi file and store events
            {

                PluginLog.Information($"Song '{songName}' not in HSC cache. adding");

                var midiFile = Managers.PlaylistManager.LoadMidiFile(index);
                var tempoMap = midiFile.GetTempoMap();

                if (midiFile == null)
                {
                    // delete file if can't be loaded(likely to be deleted locally)
                    PluginLog.Debug($"[LoadPlayback] removing {index}");
                    //PluginLog.Debug($"[LoadPlayback] removing {PlaylistManager.FilePathList[index].path}");
                    Managers.PlaylistManager.FilePathList.RemoveAt(index);
                    return false;
                }

                //fetch timed events 
                var timedEvs = global::HSC.Midi.PlaybackUtilities.GetTimedEvents(midiFile);

                //store in cache
                SongCache.AddOrUpdate(songName, (tempoMap, timedEvs));

                IPC.PlaylistManager.ApplySettings(true);//this should allow the HSC playlist to be looped 

                CurrentPlayback = global::HSC.Midi.PlaybackUtilities.GetProcessedPlayback(timedEvs, tempoMap, songName);
            }//song cached? fetch from cache and prepare playback
            else
            {
                IPC.PlaylistManager.ApplySettings(true);//this should allow the HSC playlist to be looped 

                CurrentPlayback = global::HSC.Midi.PlaybackUtilities.GetCachedPlayback(songName);
            }
        }

        IpcManager.LoadFinished(index, CurrentPlayback.GetDuration<MetricTimeSpan>().TotalMicroseconds);


        Managers.PlaylistManager.CurrentPlaying = index;

        if (switchInstrument)
        {
            try
            {
                PerformanceHelpers.SwitchInstrumentFromSong();
            }
            catch (Exception e)
            {
                PluginLog.Warning(e.ToString());
            }
        }

        //PrepareLyrics(index);//dont forget this!

        if (DalamudApi.api.PartyList.IsInParty() && Configuration.config.UseSendReadyCheck)//dont start the song if we are sending a ready check
            return true;

        if (HSC.CurrentInstrument != 0 && (wasPlaying || startPlaying))
        {
            Control.MidiControl.MidiPlayerControl.DoPlay();


            if (Settings.PrevTime != null)//jump back to the position before the song restarted e.g after chord trimming if we should
                CurrentPlayback.MoveToTime(Settings.PrevTime);

            Settings.PrevTime = null;
        }

        return true;
    }


    private static void CurrentPlayback_Started(object sender, EventArgs e)
    {
        StartTimer();

        IpcManager.PlaybackStarted(Managers.PlaylistManager.CurrentPlaying);
    }

    private static void CurrentPlayback_Stopped(object sender, EventArgs e)
    {

        StopTimer();

        if (MidiPlayerControl.playbackStatus == PlaybackStatus.Paused)
            IpcManager.Paused();
        else
            IpcManager.Stopped();

   
        //CurrentPlayback.Started -= CurrentPlayback_Started;
        //CurrentPlayback.Stopped -= CurrentPlayback_Stopped;
    }

    private static void CurrentPlayback_Finished(object sender, EventArgs e)
    {

        StopTimer();

        IpcManager.PlaybackFinished();


        Task.Run(() =>
        {
            try
            {
                if (global::HSC.HSC.AgentMetronome.EnsembleModeRunning)
                {
                    if (Configuration.config.UseCloseOnFinish)
                    {
                        PerformanceHelpers.WaitUntilChanged((Func<bool>)(() => (bool)!global::HSC.HSC.AgentMetronome.EnsembleModeRunning), 100, 5000);
                        PerformanceHelpers.ClosePerformance();
                    }
                }
                else
                {
                    if (Configuration.config.UseCloseOnFinish)
                        PerformanceHelpers.ClosePerformance();
                }

                FilePlayback.PerformWaiting(Configuration.config.SecondsBetweenTracks);
                if (needToCancel)
                {
                    needToCancel = false;
                    return;
                }

                switch ((PlayMode)Configuration.config.PlayMode)
                {
                    case PlayMode.Single:
                        break;

                    case PlayMode.SingleRepeat:
                        global::HSC.HSC.CurrentPlayback.MoveToStart();
                        Control.MidiControl.MidiPlayerControl.DoPlay();
                        break;

                    case PlayMode.ListOrdered:
                        if (Managers.PlaylistManager.CurrentPlaying + 1 < Managers.PlaylistManager.FilePathList.Count)
                        {
                            LoadPlayback(Managers.PlaylistManager.CurrentPlaying + 1, true);
                        }

                        break;

                    case PlayMode.ListRepeat:
                        if (Managers.PlaylistManager.CurrentPlaying + 1 < Managers.PlaylistManager.FilePathList.Count)
                        {
                            LoadPlayback(Managers.PlaylistManager.CurrentPlaying + 1, true);
                        }
                        else
                        {
                            LoadPlayback(0, true);
                        }

                        break;

                    case PlayMode.Random:

                        if (Managers.PlaylistManager.FilePathList.Count == 1)
                        {
                            global::HSC.HSC.CurrentPlayback.MoveToStart();
                            break;
                        }

                        try
                        {
                            var r = new Random();
                            int nexttrack;
                            do
                            {
                                nexttrack = r.Next(0, Managers.PlaylistManager.FilePathList.Count);
                            } while (nexttrack == Managers.PlaylistManager.CurrentPlaying);

                            LoadPlayback(nexttrack, true);
                        }
                        catch (Exception exception)
                        {
                            PluginLog.Error(exception, "error when random next");
                        }

                        break;

                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            catch (Exception exception)
            {
                PluginLog.Error(exception, "Unexpected exception when Playback finished.");
            }
        });
    }

    //    private static void PrepareLyrics(int index)
    //    {


    //        string[] pathArray = Managers.PlaylistManager.FilePathList[index].path.Split("\\");
    //        string LrcPath = "";
    //        string fileName = Path.GetFileNameWithoutExtension(Managers.PlaylistManager.FilePathList[index].path) + ".lrc";
    //        for (int i = 0; i < pathArray.Length - 1; i++)
    //        {
    //            LrcPath += pathArray[i];
    //            LrcPath += "\\";
    //        }

    //        LrcPath += fileName;
    //        Lrc lrc = Lrc.InitLrc(LrcPath);
    //        MidiPlayerControl.LrcTimeStamps = Lrc._lrc.LrcWord.Keys.ToList();
    //#if DEBUG
    //            PluginLog.LogVerbose($"Title: {lrc.Title}, Artist: {lrc.Artist}, Album: {lrc.Album}, LrcBy: {lrc.LrcBy}, Offset: {lrc.Offset}");
    //            foreach (var pair in lrc.LrcWord)
    //            {
    //                PluginLog.LogVerbose($"{pair.Key}, {pair.Value}");
    //            }

    //#endif
    //    }

    private static bool needToCancel { get; set; } = false;

    internal static void PerformWaiting(float seconds)
    {
        waitStart = DateTime.Now;
        waitUntil = DateTime.Now.AddSeconds(seconds);
        while (DateTime.Now < waitUntil)
        {
            Thread.Sleep(10);
        }

        waitStart = null;
        waitUntil = null;
    }

    internal static void CancelWaiting()
    {
        waitStart = null;
        waitUntil = null;
        needToCancel = true;
    }

    internal static void StopWaiting()
    {
        waitStart = null;
        waitUntil = null;
    }
}