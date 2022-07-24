using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HSC.Models.Settings
{
    /// <summary>
    /// author:  SpuriousSnail86
    /// </summary>
    [JsonObject(MemberSerialization.OptOut)]
    public class TrackSettings
    {

        public TrackSettings()
        {
            PopulateFromPlaylist = true;
        }

        public bool PopulateFromPlaylist { get; set; }
        public bool PopulateFromMidi { get; set; }
        public bool TransposeDrums { get; set; }
        public bool TransposeInstruments { get; set; }
        public bool TransposeFromTitle { get; set; }

        public bool EnableChooseDrums { get; set; }

    }
}
