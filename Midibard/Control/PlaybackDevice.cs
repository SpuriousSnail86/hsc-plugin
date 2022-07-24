using System;
using System.Linq;
using Dalamud.Logging;
using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Multimedia;
using HSC.Util;
using HSC.Config;
using HSC.Memory;
using HSC.IPC;
using HSC.Models.Settings;
using System.Collections.Generic;
using HSC.Midi;

namespace HSC.Control;

/// <summary>
/// author: akira045/Ori, modified by SpuriousSnail86
/// modified to broadcast midi events to other clients during ensemble playback
/// </summary>
 internal partial class PlaybackDevice : IOutputDevice
{
    internal struct ChannelState
    {
        public SevenBitNumber Program { get; set; }

        public ChannelState(SevenBitNumber? program)
        {
            this.Program = program ?? SevenBitNumber.MinValue;
        }
    }

    public readonly ChannelState[] Channels;
    public FourBitNumber CurrentChannel;

    public event EventHandler<MidiEventSentEventArgs> EventSent;

    public PlaybackDevice()
    {
        Channels = new ChannelState[16];
        CurrentChannel = FourBitNumber.MinValue;
    }

    public void PrepareForEventsSending()
    {
    }

    /// <summary>
    /// Midi events send from input device
    /// </summary>
    /// <param name="midiEvent">Raw midi event</param>
    public void SendEvent(MidiEvent midiEvent)
    {
        SendEventWithMetadata(midiEvent, null);
    }

    record MidiEventMetaData
    {
        public enum EventSource
        {
            Playback,
            MidiListener
        }

        public int TrackIndex { get; init; }
        public EventSource Source { get; init; }
    }

    /// <summary>
    /// Directly invoked by midi events sent from file playback
    /// </summary>
    /// <param name="midiEvent">Raw midi event</param>
    /// <param name="metadata">Currently is track index</param>
    /// <returns></returns>
    public bool SendEventWithMetadata(MidiEvent midiEvent, object metadata)
    {
        try
        {
            var trackIndex = (int?)metadata;

            if (!trackIndex.HasValue)
                return false;

            if (!HSC.AgentPerformance.InPerformanceMode) return false;

            if (!Settings.EnabledTracks[trackIndex.Value])
                return false;

            if (midiEvent is NoteOnEvent noteOnEvent)
            {
                if (HSC.PlayingGuitar)
                {
                    switch (Configuration.config.GuitarToneMode)
                    {
                        case GuitarToneMode.Off:
                            break;
                        case GuitarToneMode.Standard:
                            HandleToneSwitchEvent(noteOnEvent);
                            break;
                        case GuitarToneMode.Override:
                            {
                                int tone = Configuration.config.TonesPerTrack[trackIndex.Value];
                                playlib.GuitarSwitchTone(tone);

                                // PluginLog.Verbose($"[N][NoteOn][{trackIndex}:{noteOnEvent.Channel}] Overriding guitar tone {tone}");
                                break;
                            }
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
            }

            return SendMidiEvent(midiEvent, trackIndex);
        }
        catch (Exception ex)
        {
            return false;
        }
    }

    void HandleToneSwitchEvent(NoteOnEvent noteOnEvent)
    {

        CurrentChannel = noteOnEvent.Channel;
        // }
        SevenBitNumber program = Channels[CurrentChannel].Program;
        if (HSC.ProgramInstruments.TryGetValue(program, out var instrumentId))
        {
            var instrument = HSC.Instruments[instrumentId];
            if (instrument.IsGuitar)
            {
                int tone = instrument.GuitarTone;
                playlib.GuitarSwitchTone(tone);
            }
        }
    }

    unsafe bool SendMidiEvent(MidiEvent midiEvent, int? trackIndex)
    {
        switch (midiEvent)
        {
            case ProgramChangeEvent ev:
                return HandleProgramChange(ev);
            case NoteOnEvent noteOnEvent:
                return HandleNoteOn(noteOnEvent.NoteNumber, trackIndex.Value, noteOnEvent.Channel);
            case NoteOffEvent noteOffEvent:
                return HandleNoteOff(noteOffEvent.NoteNumber, trackIndex.Value, noteOffEvent.Channel);
        }

        return false;
    }


    private bool HandleProgramChange(ProgramChangeEvent ev)
    {
        switch (Configuration.config.GuitarToneMode)
        {
            case GuitarToneMode.Off:
                break;
            case GuitarToneMode.Standard:
                Channels[ev.Channel].Program = ev.ProgramNumber;
                break;
            case GuitarToneMode.Simple:
                Array.Fill(Channels, new ChannelState(ev.ProgramNumber));
                break;
            case GuitarToneMode.Override:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        return true;
    }


    private static unsafe bool HandleNoteOn(int note, int trackIndex, FourBitNumber channel)
    {
        var noteNum = GetTransposedNoteNum(note, (int)channel);

        if (noteNum is < 0 or > 36)
            return false;

        if (HSC.AgentPerformance.noteNumber - 39 == noteNum)
        {
            // release repeated note in order to press it again

            if (playlib.ReleaseKey(noteNum))
                HSC.AgentPerformance.Struct->PressingNoteNumber = -100;
        }

        if (playlib.PressKey(noteNum, ref HSC.AgentPerformance.Struct->NoteOffset,
                ref HSC.AgentPerformance.Struct->OctaveOffset))
        {
            HSC.AgentPerformance.Struct->PressingNoteNumber = noteNum + 39;
        }

        return true;
    }

    private static unsafe bool HandleNoteOff(int note, int trackIndex, FourBitNumber channel)
    {

        var noteNum = GetTransposedNoteNum(note, (int)channel);

        if (noteNum is < 0 or > 36) 
            return false;


        if (HSC.AgentPerformance.Struct->PressingNoteNumber - 39 != noteNum)
            return false;

        // only release a key when it been pressing
        if (playlib.ReleaseKey(noteNum))
        {
            HSC.AgentPerformance.Struct->PressingNoteNumber = -100;

        }
        return true;
    }

    //private static int GetInstrumentNoteDiff(int ins)
    //{
    //    if (ins == 0)
    //        return 0;

    //    global::HSC.Instrument instrument = (global::HSC.Instrument)ins;
    //    string insName = instrument.ToString();

    //    if (!Settings.InstrumentShifts.Offsets.ContainsKey(insName))
    //        return 0;

    //    return Settings.InstrumentShifts.Offsets[instrument.ToString()] * 12;
    //}


    private static int GetTransposedNoteNum(int noteNum, int channel)
    {
        if (channel == 9 && DrumMapper.IsDrumNote(noteNum))
            return DrumMapper.MapNote(noteNum);

        noteNum = noteNum - 48;

        if (Configuration.config.autoAdaptNotes)
        {
            if (noteNum < 0)
            {
                noteNum = (noteNum + 1) % 12 + 11;
            }
            else if (noteNum > 36)
            {
                noteNum = (noteNum - 1) % 12 + 25;
            }
        }

        return noteNum;
    }
}