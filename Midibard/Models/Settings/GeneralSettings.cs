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
    public class GeneralSettings
    {

        public GeneralSettings()
        {
            EnableTranspose = true;
        }

        public bool EnableTranspose { get; set; }

        public bool EnableTrim { get; set; }

        public bool EnableTrimFromTracks { get; set; }

        public bool EnableInstrumentSwitching { get; set; }

        public bool CloseOnFinish { get; set; }

        public bool SendReadyCheckOnEquip { get; set; }

        public bool AutoPlayOnSelect { get; set; }

    }
}
