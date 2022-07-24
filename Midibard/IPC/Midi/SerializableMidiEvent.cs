using HSC.Control.MidiControl.PlaybackInstance;
using Melanchall.DryWetMidi.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HSC.IPC.Midi
{
    [ProtoBuf.ProtoContract()]
    internal class SerializableMidiEvent
    {
        public SerializableMidiEvent(MidiEventType type, long deltaTime)
        {
            Type = type;
            DeltaTime = deltaTime;
        }

        public MidiEventType Type { get; set; }

        public long DeltaTime { get; set; }

        public static SerializableMidiEvent From(MidiEvent ev) => new SerializableMidiEvent(ev.EventType, ev.DeltaTime);
    }
}
