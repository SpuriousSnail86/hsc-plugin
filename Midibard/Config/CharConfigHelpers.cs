
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HSC.Common;

using Dalamud.Logging;
using HSC.Helpers;
using HSC.Models.Settings;

namespace HSC.Config
{

    /// <summary>
    /// author:  SpuriousSnail86
    /// </summary>
    public class CharConfigHelpers
    {
        public const string CharConfigFileName = "characters.config";

        public static void Load()
        {
            string filePath = Path.Join(DalamudApi.api.PluginInterface.GetPluginConfigDirectory(), CharConfigFileName);
            Settings.CharConfig = FileHelpers.Load<CharacterConfig>(filePath);
        }

        public static void UpdateCharIndex(string charName)
        {
            Load();

            if (Settings.CharConfig == null || string.IsNullOrEmpty(charName))
                return;

            var chars = Settings.CharConfig.ToDictionary();

            Settings.CharIndex = chars.IsNullOrEmpty() || !chars.ContainsKey(charName) ? -1 : chars[charName];
        }
    }
}
