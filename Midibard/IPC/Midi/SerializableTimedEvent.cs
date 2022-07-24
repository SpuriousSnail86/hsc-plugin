using HSC.Control.MidiControl.PlaybackInstance;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HSC.IPC.Midi
{
    [ProtoBuf.ProtoContract()]
    internal class SerializableTimedEvent
    {
        public SerializableTimedEvent(int trackIndex, long time, SerializableMidiEvent @event)
        {
            TrackIndex = trackIndex;
            Time = time;
            Event = @event;
        }

        public int TrackIndex { get; set; }
        public long Time { get; set; }
        public SerializableMidiEvent Event { get; set; }

        public static SerializableTimedEvent From(TimedEventWithTrackChunkIndex timedEv) => new SerializableTimedEvent((int)timedEv.Metadata, timedEv.Time, SerializableMidiEvent.From(timedEv.Event));
    }
}
