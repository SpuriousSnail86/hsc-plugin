using System.Collections.Generic;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Multimedia;
using Melanchall.DryWetMidi.Interaction;

namespace HSC.Control.MidiControl.PlaybackInstance;

/// <summary>
/// author: akira045/Ori
/// </summary>
public sealed class BardPlayback : Playback
{
    public BardPlayback(IEnumerable<ITimedObject> timedObjects, TempoMap tempoMap, MidiClockSettings clockSettings) : base(timedObjects, tempoMap, new PlaybackSettings(){ ClockSettings = clockSettings})
    {

    }
		
    protected override bool TryPlayEvent(MidiEvent midiEvent, object metadata)
    {
        // Place your logic here
        // Return true if event played (sent to plug-in); false otherwise
        return HSC.CurrentOutputDevice.SendEventWithMetadata(midiEvent, metadata);
    }
}