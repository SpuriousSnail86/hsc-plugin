using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace HSC.Models.Settings
{

    [JsonObject(MemberSerialization.OptOut)]
    public class PlaylistSettings
    {

        public PlaylistSettings()
        {

        }

        public int RepeatMode { get; set; }

    }
}
