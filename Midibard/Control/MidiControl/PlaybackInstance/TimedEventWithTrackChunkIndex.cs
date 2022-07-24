using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;

namespace HSC.Control.MidiControl.PlaybackInstance;

/// <summary>
/// author: akira045/Ori, modified by SpuriousSnail86
/// </summary>


public class TimedEventWithTrackChunkIndex : TimedEvent, IMetadata
{
    public TimedEventWithTrackChunkIndex Copy() => new TimedEventWithTrackChunkIndex(this.Event.Clone(), this.Time, (int)this.Metadata);

    public TimedEventWithTrackChunkIndex(MidiEvent midiEvent, long time, int trackChunkIndex)
        : base(midiEvent.Clone(), time)
    {
        Metadata = trackChunkIndex;
    }

    public object Metadata { get; set; }
}