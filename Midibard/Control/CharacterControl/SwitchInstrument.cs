using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Dalamud.Interface.Internal.Notifications;
using Dalamud.Logging;
using Lumina.Excel.GeneratedSheets;
using HSC.Managers;
using HSC.Managers.Agents;
using HSC.Control.MidiControl;
using HSC.Common;

namespace HSC.Control.CharacterControl
{
    /// <summary>
    /// author: akira045/Ori
    /// </summary>
    internal static class SwitchInstrument
    {
        public static bool SwitchingInstrument { get; set; }


        private static Regex regex = new Regex(@"^#(?<ins>.*?)(?<trans>[-|+][0-9]+)?#(?<name>.*)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static string ParseSongName(string inputString, out uint? instrumentId, out int? transpose)
        {
            var match = regex.Match(inputString);
            if (match.Success)
            {
                var capturedInstrumentString = match.Groups["ins"].Value;
                var capturedTransposeString = match.Groups["trans"].Value;
                var capturedSongName = match.Groups["name"].Value;

                PluginLog.Debug($"input: \"{inputString}\", instrumentString: {capturedInstrumentString}, transposeString: {capturedTransposeString}");
                transpose = int.TryParse(capturedTransposeString, out var t) ? t : null;
                instrumentId = TryParseInstrumentName(capturedInstrumentString, out var id) ? id : null;
                return !string.IsNullOrEmpty(capturedSongName) ? capturedSongName : inputString;
            }

            instrumentId = null;
            transpose = null;
            return inputString;
        }

        public static bool TryParseInstrumentName(string capturedInstrumentString, out uint instrumentId)
        {
            Perform equal = HSC.InstrumentSheet.FirstOrDefault(i =>
            i?.Instrument?.RawString.Equals(capturedInstrumentString, StringComparison.InvariantCultureIgnoreCase) == true);
            Perform contains = HSC.InstrumentSheet.FirstOrDefault(i =>
            i?.Instrument?.RawString?.ContainsIgnoreCase(capturedInstrumentString) == true);
            Perform gmName = HSC.InstrumentSheet.FirstOrDefault(i =>
            i?.Name?.RawString?.ContainsIgnoreCase(capturedInstrumentString) == true);

            var rowId = (equal ?? contains ?? gmName)?.RowId;
            PluginLog.Debug($"equal: {equal?.Instrument?.RawString}, contains: {contains?.Instrument?.RawString}, gmName: {gmName?.Name?.RawString} finalId: {rowId}");
            if (rowId is null)
            {
                instrumentId = 0;
                return false;
            }
            else
            {
                instrumentId = rowId.Value;
                return true;
            }
        }


        internal static void UpdateGuitarToneByConfig()
        {
            //if (MidiBard.CurrentTracks == null)
            //{
            //    return;
            //}

            //for (int track = 0; track < MidiBard.CurrentTracks.Count; track++)
            //{
            //    if (ConfigurationPrivate.config.EnabledTracks[track] && MidiBard.CurrentTracks[track].trackInfo != null)
            //    {
            //        var curInstrument = MidiBard.CurrentTracks[track].trackInfo?.InstrumentIDFromTrackName;
            //        if (curInstrument != null && MidiBard.guitarGroup.Contains((byte)curInstrument))
            //        {
            //            var toneID = curInstrument - MidiBard.guitarGroup[0];
            //            Configuration.config.TonesPerTrack[track] = (int)toneID;
            //        }
            //    }
            //}
        }
    }
}