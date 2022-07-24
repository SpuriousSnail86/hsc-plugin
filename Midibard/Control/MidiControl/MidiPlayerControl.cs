using System;
using System.Linq;
using System.Threading.Tasks;
using Dalamud.Logging;
using Melanchall.DryWetMidi.Interaction;
using HSC.Control.CharacterControl;
using static HSC.HSC;
using HSC.Managers;
using System.Collections.Generic;
using FFXIVClientStructs.FFXIV.Client.Game.Group;
using HSC.Config;
using HSC.Common;
using HSC.IPC;
using HSC.Models.Settings;

namespace HSC.Control.MidiControl
{
    
/// <summary>
/// author: akira045/Ori, modified by SpuriousSnail86
/// </summary>
    internal static class MidiPlayerControl
    {
        internal static int playDeltaTime = 0;
        internal static int LRCDeltaTime = 0;

        public enum PlaybackStatus
        {
            Stopped,
            Paused,
            Playing
        }

        public static PlaybackStatus playbackStatus = PlaybackStatus.Stopped;
        public static int LrcIdx = -1;
        public static bool IsLeader;

        public static List<double> LrcTimeStamps = new List<double>();

        public static bool LrcLoaded()
        {
            return IsLeader && LrcTimeStamps.Count > 0;
        }

        public static void Tick(Dalamud.Game.Framework framework)
        {
            try
            {
                if (playbackStatus != PlaybackStatus.Playing)
                {
                    return;
                }

                if (LrcTimeStamps.Count > 0 && LrcIdx < LrcTimeStamps.Count)
                {
                    int idx = FindLrcIdx(LrcTimeStamps);
                    if (idx < 0 || idx == LrcIdx)
                    {
                        return;
                    }
                    else
                    {
                        if (IsLeader)
                        {
                            string msg = "";
                            if (idx == 0)
                            {
                                msg = $"♪ Now Playing: {Lrc._lrc.Title} ♪ Artist: {Lrc._lrc.Artist} ♪ Album: {Lrc._lrc.Album} ♪ Lyric By: {Lrc._lrc.LrcBy} ♪";
                                if (!AgentMetronome.EnsembleModeRunning)
                                {
                                    msg = "/p " + msg;
                                }
                            }
                            else
                            {
                                PluginLog.LogVerbose($"{Lrc._lrc.LrcWord[LrcTimeStamps[idx]]}");
                                if (AgentMetronome.EnsembleModeRunning)
                                {
                                    msg = $"/s ♪ {Lrc._lrc.LrcWord[LrcTimeStamps[idx]]} ♪";
                                } else
                                {
                                    msg = $"/p ♪ {Lrc._lrc.LrcWord[LrcTimeStamps[idx]]} ♪";
                                }
                            }

                            HSC.Cbase.Functions.Chat.SendMessage(msg);
                        }
                        LrcIdx = idx;
                    }
                }
            } catch (Exception ex) {
                PluginLog.LogError($"exception: {ex}");
            }
        }

        static int FindLrcIdx(List<double> TimeStamps)
        {
            if (TimeStamps.Count == 0)
                return -1;

            int idx = -1;
            double timeSpan = CurrentPlayback.GetCurrentTime<MetricTimeSpan>().GetTotalSeconds() - Lrc._lrc.Offset / 1000.0f + LRCDeltaTime / 1000.0f;
            foreach (double TimeStamp in TimeStamps)
            {
                if (timeSpan > TimeStamp)
                {
                    idx++;
                }
                else
                {
                    break;
                }
            }

            return idx >= TimeStamps.Count ? -1 : idx;
        }

        internal static void Play()
        {
            try
            {
                playDeltaTime = 0;
                LRCDeltaTime = 0;
                if (CurrentPlayback != null)
                {

                    if (CurrentPlayback.GetCurrentTime<MidiTimeSpan>() == CurrentPlayback.GetDuration<MidiTimeSpan>())
                    {
                        CurrentPlayback.MoveToStart();
                    }

                    DoPlay();
                }

                else
                {
                    if (!Managers.PlaylistManager.FilePathList.Any())
                    {
                        PluginLog.Information("empty playlist");
                        return;
                    }

                    if (Managers.PlaylistManager.CurrentPlaying < 0)
                    {
                        SwitchSong(0, true);
                    }
                    else
                    {
                        SwitchSong(Managers.PlaylistManager.CurrentPlaying, true);
                    }
                }
            }
            catch (Exception e)
            {
                PluginLog.Error(e, "error when try to start playing, maybe the playback has been disposed?");
            }

        }

        public static void DoPlay()
        {
            if (CurrentPlayback == null)
            {
                return;
            }

            IsLeader = false;

            //if (Lrc.HasLyric())
            //{
            //    if (DalamudApi.api.PartyList.Length > 1)
            //    {
            //        // it seems the PartyLeaderIndex always == 0 after joined and leave a party, 
            //        var partyMemberAddr = DalamudApi.api.PartyList.GetPartyMemberAddress((int)DalamudApi.api.PartyList.PartyLeaderIndex);
            //        if (partyMemberAddr != IntPtr.Zero)
            //        {
            //            var partymember = DalamudApi.api.PartyList.CreatePartyMemberReference(partyMemberAddr);
            //            IsLeader = String.Compare(partymember.Name.TextValue, DalamudApi.api.ClientState.LocalPlayer.Name.TextValue) == 0;
            //        }
            //    }
            //    else
            //    {
            //        DalamudApi.api.ChatGui.Print(String.Format("[MidiBard] Not in a party, Lyrics will not be posted."));
            //    }
            //}

            CurrentPlayback.Start();
            //try
            //{
            //    LrcTimeStamps = Lrc._lrc.LrcWord.Keys.ToList();
            //    if (_stat != e_stat.Paused)
            //    {
            //        LrcIdx = -1;
            //    }
            //}
            //catch (Exception e)
            //{
            //    PluginLog.LogError(e.Message);
            //}
            //_stat = e_stat.Playing;

        }

        internal static void Pause()
        {
            playbackStatus = PlaybackStatus.Paused;
            CurrentPlayback?.Stop();
        }


        internal static void PlayPause()
        {
            if (CurrentPlayback == null)
            {
                return;
            }

            var TimeSpan = CurrentPlayback.GetCurrentTime<MetricTimeSpan>();
            PluginLog.LogInformation($"Timespan: [{TimeSpan.Minutes}:{TimeSpan.Seconds}:{TimeSpan.Milliseconds}]");

            if (FilePlayback.isWaiting)
            {
                FilePlayback.StopWaiting();
            }
            else
            {
                if (HSC.IsPlaying)
                {
                    MidiPlayerControl.Pause();
                }
                else
                {
                    MidiPlayerControl.Play();
                }
            }


        }

        internal static void Stop()
        {
            try
            {
                playbackStatus = PlaybackStatus.Stopped;

                if (CurrentPlayback == null)
                {
                    CurrentTracks.Clear();
                }
                else
                {
                    CurrentPlayback?.Stop();
                    CurrentPlayback?.MoveToTime(new MidiTimeSpan(0));
                }
            }
            catch (Exception e)
            {
                PluginLog.Warning("Already stopped!");
            }
            finally
            {
                LrcIdx = -1;
                CurrentPlayback?.Dispose();
                CurrentPlayback = null;
                IsLeader = false;
            }

        }

        internal static void Next()
        {
            LrcIdx = -1;
            playbackStatus = PlaybackStatus.Stopped;
            if (CurrentPlayback != null)
            {
                try
                {
                    var playing = IsPlaying;
                    CurrentPlayback?.Dispose();
                    CurrentPlayback = null;
                    int next = Managers.PlaylistManager.CurrentPlaying;

                    switch ((PlayMode)Configuration.config.PlayMode)
                    {
                        case PlayMode.Single:
                        case PlayMode.SingleRepeat:
                        case PlayMode.ListOrdered:
                            next += 1;
                            break;
                        case PlayMode.ListRepeat:
                            next = (next + 1) % Managers.PlaylistManager.FilePathList.Count;
                            break;
                        case PlayMode.Random:
                            if (Managers.PlaylistManager.FilePathList.Count > 1)
                            {
                                var r = new Random();
                                do
                                {
                                    next = r.Next(0, Managers.PlaylistManager.FilePathList.Count);
                                } while (next == Managers.PlaylistManager.CurrentPlaying);
                            }
                            break;
                    }

                    SwitchSong(next, playing);
                }
                catch (Exception e)
                {
                    CurrentPlayback = null;
                    Managers.PlaylistManager.CurrentPlaying = -1;
                }
            }
            else
            {
                Managers.PlaylistManager.CurrentPlaying += 1;
            }
        }

        internal static void Prev()
        {
            LrcIdx = -1;
            playbackStatus = PlaybackStatus.Stopped;
            if (CurrentPlayback != null)
            {
                try
                {
                    var playing = IsPlaying;
                    CurrentPlayback?.Dispose();
                    CurrentPlayback = null;
                    int prev = Managers.PlaylistManager.CurrentPlaying;

                    switch ((PlayMode)Configuration.config.PlayMode)
                    {
                        case PlayMode.Single:
                        case PlayMode.SingleRepeat:
                        case PlayMode.ListOrdered:
                            prev -= 1;
                            break;
                        case PlayMode.ListRepeat:
                            if (Managers.PlaylistManager.CurrentPlaying == 0)
                            {
                                prev = Managers.PlaylistManager.FilePathList.Count - 1;
                            }
                            else
                            {
                                prev -= 1;
                            }
                            break;
                        case PlayMode.Random:
                            if (Managers.PlaylistManager.FilePathList.Count > 1)
                            {
                                var r = new Random();
                                do
                                {
                                    prev = r.Next(0, Managers.PlaylistManager.FilePathList.Count);
                                } while (prev == Managers.PlaylistManager.CurrentPlaying);
                            }
                            break;
                    }
                  
                    SwitchSong(prev, playing);
                }
                catch (Exception e)
                {
                    CurrentPlayback = null;
                    Managers.PlaylistManager.CurrentPlaying = -1;
                }
            }
            else
            {
                Managers.PlaylistManager.CurrentPlaying -= 1;
            }
        }

        internal static void MoveTime(double timeInSeconds)
        {
            try
            {
                var metricTimeSpan = CurrentPlayback.GetCurrentTime<MetricTimeSpan>();
                var dura = CurrentPlayback.GetDuration<MetricTimeSpan>();
                var totalMicroseconds = metricTimeSpan.TotalMicroseconds + (long)(timeInSeconds * 1_000_000);
                if (totalMicroseconds < 0) totalMicroseconds = 0;
                if (totalMicroseconds > dura.TotalMicroseconds) totalMicroseconds = dura.TotalMicroseconds;
                CurrentPlayback.MoveToTime(new MetricTimeSpan(totalMicroseconds));
            }
            catch (Exception e)
            {
                PluginLog.Warning(e.ToString(), "error when try setting current playback time");
            }
        }

        public static void SetSpeed(float speed)
        {
            Configuration.config.PlaySpeed = speed;
            SetSpeed();
        }

        public static void SetSpeed()
        {
            Configuration.config.PlaySpeed = Math.Max(0.1f, Configuration.config.PlaySpeed);
            var currenttime = HSC.CurrentPlayback?.GetCurrentTime(TimeSpanType.Midi);
            if (currenttime is not null)
            {
                HSC.CurrentPlayback.Speed = Configuration.config.PlaySpeed;
                HSC.CurrentPlayback?.MoveToTime(currenttime);
            }
        }

        public static void SwitchSong(int index, bool startPlaying = false, bool switchInstruments = true)
        {
            try
            {
                LrcIdx = -1;
                playbackStatus = PlaybackStatus.Stopped;
                playDeltaTime = 0;
                LRCDeltaTime = 0;
                if (index < 0 || index >= Managers.PlaylistManager.FilePathList.Count)
                {
                    PluginLog.Error($"SwitchSong: invalid playlist index {index}");
                    return;
                }

                Managers.PlaylistManager.CurrentPlaying = index;
                Task.Run(() => FilePlayback.LoadPlayback(Managers.PlaylistManager.CurrentPlaying, startPlaying, switchInstruments));
            }
            catch (Exception ex)
            {

            }
        }

        internal static void ChangeTime(int hours, int mins, int secs)
        {


            if (CurrentPlayback == null || !CurrentPlayback.IsRunning)
            {
                playDeltaTime = 0;
                LRCDeltaTime = 0;
                return;
            }

            MetricTimeSpan newTime = new MetricTimeSpan(hours, mins, secs);
            //PluginLog.LogDebug("newTime:" + newTime.TotalMicroseconds);
            CurrentPlayback.MoveToTime(newTime);

            playDeltaTime = newTime.Seconds;
        }

        internal static void ChangeTime(long ms)
        {
            if (CurrentPlayback == null || !CurrentPlayback.IsRunning)
            {
                playDeltaTime = 0;
                LRCDeltaTime = 0;
                return;
            }

            MetricTimeSpan newTime = new MetricTimeSpan(TimeSpan.FromMilliseconds(ms));
            //PluginLog.LogDebug("newTime:" + newTime.TotalMicroseconds);
            CurrentPlayback.MoveToTime(newTime);

            playDeltaTime = newTime.Seconds;
        }

        internal static void ChangeDeltaTime(int delta)
        {
            if (CurrentPlayback == null || !CurrentPlayback.IsRunning)
            {
                playDeltaTime = 0;
                LRCDeltaTime = 0;
                return;
            }

            var currentTime = CurrentPlayback.GetCurrentTime<MetricTimeSpan>();
            long msTime = currentTime.TotalMicroseconds;
            //PluginLog.LogDebug("curTime:" + msTime);
            if (msTime + delta * 1000 < 0)
            {
                return;
            }
            msTime += delta * 1000;
            MetricTimeSpan newTime = new MetricTimeSpan(msTime);
            //PluginLog.LogDebug("newTime:" + newTime.TotalMicroseconds);
            CurrentPlayback.MoveToTime(newTime);
            playDeltaTime += delta;
        }

        internal static void ChangeLRCDeltaTime(int delta)
        {
            if (CurrentPlayback == null || !CurrentPlayback.IsRunning)
            {
                playDeltaTime = 0;
                LRCDeltaTime = 0;
                return;
            }
          
            LRCDeltaTime += delta;
        }
    }
}
