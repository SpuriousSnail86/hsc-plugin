using Dalamud.Logging;
using HSC.Common;
using HSC.Helpers;
using HSC.Models.Settings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls.Primitives;

namespace HSC.IPC
{
    /// <summary>
    /// author:  SpuriousSnail86
    /// </summary>
    internal class IpcHelpers
    {
        internal static int? GetClientIndexFromTrack(int trackIndex) => Settings.ClientIndexes.ContainsKey(trackIndex) ? Settings.ClientIndexes[trackIndex] : null;
    }
}
