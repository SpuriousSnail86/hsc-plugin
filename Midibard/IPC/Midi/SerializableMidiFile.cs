using HSC.Control.MidiControl.PlaybackInstance;
using Melanchall.DryWetMidi.Interaction;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HSC.IPC.Midi
{
    [ProtoBuf.ProtoContract()]
    internal class SerializableMidiFile
    {
        public List<SerializableTimedEvent> TimedEvents { get; set; }

        //public static SerializableMidiFile From(IEnumerable<TimedEventWithTrackChunkIndex> timedEvs, TempoMap tempoMap) => new SerializableMidiFile() {  TimedEvents = timedEvs, TempoMap = SerializableTempoMap.From }
    }
}
