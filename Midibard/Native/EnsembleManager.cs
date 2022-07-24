using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dalamud.Hooking;
using Dalamud.Logging;
using Melanchall.DryWetMidi.Multimedia;
using Melanchall.DryWetMidi.Interaction;
using HSC.Control.MidiControl;
using HSC.Managers.Agents;
using static HSC.HSC;
using HSC.Managers;
using HSC.Config;
using HSC.Models.Settings;
using HSC.Managers.Ipc;
using HSC.Midi;
using HSC.Control;

namespace HSC.Memory
{
    /// <summary>
    /// author: akira045 - thanks for this amazing work
    /// </summary>
    internal class EnsembleManager : IDisposable
    {
        //public SyncHelper(out List<(byte[] notes, byte[] tones)> sendNotes, out List<(byte[] notes, byte[] tones)> recvNotes)
        //{
        //	sendNotes = new List<(byte[] notes, byte[] tones)>();
        //	recvNotes = new List<(byte[] notes, byte[] tones)>();
        //}

        private delegate IntPtr sub_140C87B40(IntPtr agentMetronome, byte beat);

        private Hook<sub_140C87B40> UpdateMetronomeHook;

        private static System.Timers.Timer ensembleTimer;

        Stopwatch stopWatch;

        private EnsembleManager()
        {
            UpdateMetronomeHook = new Hook<sub_140C87B40>(Offsets.UpdateMetronome, HandleUpdateMetronome);
            UpdateMetronomeHook.Enable();
        }

        private IntPtr HandleUpdateMetronome(IntPtr agentMetronome, byte currentBeat)
        {
            try
            {
                var original = UpdateMetronomeHook.Original(agentMetronome, currentBeat);

                if (Configuration.config.MonitorOnEnsemble)
                {
                    byte Ensemble;
                    byte beatsPerBar;
                    int barElapsed;

                    unsafe
                    {
                        var metronome = (AgentMetronome.AgentMetronomeStruct*)agentMetronome;
                        beatsPerBar = metronome->MetronomeBeatsPerBar;
                        barElapsed = metronome->MetronomeBeatsElapsed;
                        Ensemble = metronome->EnsembleModeRunning;

                        if (Ensemble == 0)//when ensemble drops. stop playback
                            MidiPlayerControl.Stop();
                    }


                    if (barElapsed == 0 && currentBeat == 0)
                    {
                        if (Ensemble != 0)
                        {
                            // 箭头后面是每种乐器的的延迟，所以要达成同步每种乐器需要提前于自己延迟的时间开始演奏
                            // 而提前开始又不可能， 所以把所有乐器的延迟时间减去延迟最大的鲁特琴（让所有乐器等待鲁特琴）
                            // 也就是105减去每种乐器各自的延迟
                            var compensation = 105 - HSC.CurrentInstrument switch
                            {
                                0 => 104,
                                1 => 85,
                                2 or 4 => 90,
                                3 => 104,
                                >= 5 and <= 8 => 95,
                                9 or 10 => 90,
                                11 or 12 => 80,
                                13 => 85,
                                >= 14 => 30
                            };

                            try
                            {
                                var midiClock = new MidiClock(false, new HighPrecisionTickGenerator(), TimeSpan.FromMilliseconds(compensation));
                                midiClock.Restart();
                                PluginLog.Warning($"setup midiclock compensation: {compensation}");
                                midiClock.Ticked += OnMidiClockOnTicked;
                                stopWatch.Stop();
                                //Lrc._lrc.Offset += stopWatch.ElapsedMilliseconds - compensation;
                                //PluginLog.Warning($"LRC Offset: {Lrc._lrc.Offset}");

                                void OnMidiClockOnTicked(object o, EventArgs eventArgs)
                                {
                                    try
                                    {
                                        MidiPlayerControl.DoPlay();
                                        EnsembleStart?.Invoke();
                                        PluginLog.Warning($"Start ensemble: compensation: {midiClock.CurrentTime.TotalMilliseconds} ms / {midiClock.CurrentTime.Ticks} ticks");
                                    }
                                    catch (Exception e)
                                    {
                                        PluginLog.Error(e, "error OnMidiClockOnTicked");
                                    }
                                    finally
                                    {
                                        midiClock.Ticked -= OnMidiClockOnTicked;
                                    }
                                }

                                Task.Delay(1000).ContinueWith(_ =>
                                {
                                    midiClock.Dispose();
                                    PluginLog.Information($"midi clock disposed.");
                                });
                            }
                            catch (Exception e)
                            {
                                PluginLog.Error(e, "error when starting ensemble playback");
                            }
                        }
                    }

                    if (barElapsed == -2 && currentBeat == 0)
                    {
                        PluginLog.Warning($"Prepare: ensemble: {Ensemble}");
                        if (Ensemble != 0)
                        {
                            EnsemblePrepare?.Invoke();
                            stopWatch = Stopwatch.StartNew();
                            Task.Run(() =>
                            {
                                try
                                {
                                    //if (!DalamudApi.api.PartyList.IsPartyLeader()) //leader loads the MIDI only
                                    //    return;

                                    var playing = PlaylistManager.CurrentPlaying;
                                    if (playing == -1)
                                    {
                                        FilePlayback.LoadPlayback(0, false, false);
                                    }
                                    else
                                    {
                                        FilePlayback.LoadPlayback(playing, false, false);
                                    }

                                    //MidiBard.CurrentPlayback.Stop();
                                    //MidiBard.CurrentPlayback.MoveToStart();
                                }
                                catch (Exception e)
                                {
                                    PluginLog.Error(e, "error when loading playback for ensemble");
                                }
                            });
                        }
                    }

                    //PluginLog.Verbose($"[Metronome] {barElapsed} {currentBeat}/{beatsPerBar}");
                }

                return original;
            }
            catch (Exception e)
            {
                PluginLog.Error(e, $"error in {nameof(UpdateMetronomeHook)}");
                return IntPtr.Zero;
            }
        }

        public event Action EnsembleStart;

        public event Action EnsemblePrepare;

        public static EnsembleManager Instance { get; } = new EnsembleManager();

        public void Dispose()
        {
            UpdateMetronomeHook?.Dispose();
        }
    }
}