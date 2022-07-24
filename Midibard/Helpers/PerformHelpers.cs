using Dalamud.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HSC.Common;
using HSC.Control.CharacterControl;
using System.Diagnostics;
using System.Threading;
using Dalamud.Interface.Internal.Notifications;
using HSC.DalamudApi;
using HSC.Managers.Ipc;
using HSC.Control.MidiControl;
using HSC.Memory;
using HSC.Config;
using HSC.Models.Music;
using HSC.Models.Settings;
using HSC.IPC;
using HSC.Midi;

namespace HSC.Helpers
{

    /// <summary>
    /// author:  SpuriousSnail86
    /// </summary>
    public class PerformanceHelpers
    {
        private static bool readyCheckSent;

        public static bool HasAssignedMember(Track track) => GetAssignedMember(track) != null;
        public static int? GetAssignedMember(Track track) => Settings.AppSettings.TrackSettings.PopulateFromPlaylist ? track.EnsembleMember : track.AutofilledMember;
        public static string GetAssignedInstrument(Track track) => Settings.AppSettings.TrackSettings.PopulateFromPlaylist ? track.EnsembleInstrument : track.AutofilledInstrument;
        public static bool TrackHasInstrument(Track track) => !string.IsNullOrEmpty(GetAssignedInstrument(track));

        public static Instrument GetInstrumentFromName(string insName)
        {
            if (string.IsNullOrEmpty(insName) || insName == "None")
                return Instrument.None;

            if (insName.Equals("Guitar (Overdriven)"))
                return Instrument.ElectricGuitarOverdriven;

            if (insName.Equals("Guitar (Clean)"))
                return Instrument.ElectricGuitarClean;

            if (insName.Equals("Guitar (Muted)"))
                return Instrument.ElectricGuitarMuted;

            if (insName.Equals("Guitar (Distorted)"))
                return Instrument.ElectricGuitarPowerChords;

            return (Instrument)Enum.Parse(typeof(Instrument), insName);
        }

        public static void ClosePerformance()
        {
            if (Settings.CharIndex == -1)
            {
                ImGuiUtil.AddNotification(NotificationType.Error, $"Cannot close performance mode. Character config not loaded for '{Settings.CharName}'.");
                return;
            }

            if (Settings.SwitchInstrumentFailed)
            {
                ImGuiUtil.AddNotification(NotificationType.Error, "Cannot switch instruments yet. Wait 3 seconds.");
                return;
            }

            ImGuiUtil.AddNotification(NotificationType.Info, "Closing performance mode and stopping playback.");

            if (HSC.IsPlaying)
                MidiPlayerControl.Stop();

            MidiMessageHandler.Stop();
            //ImGuiUtil.AddNotification(NotificationType.Error, "Cannot close instrument while playing.");

            if (HSC.CurrentInstrument == 0)
                return;

            PerformActions.DoPerformAction(0);
            bool success = WaitUntilChanged(() => HSC.CurrentInstrument == 0, 100, 3000);

            if (!success)
            {
                SwitchInstrumentFailed();
                PluginLog.Error($"Failed to unequip instrument.");
                return;
            }

            Thread.Sleep(200);
            ImGuiUtil.AddNotification(NotificationType.Success, $"Performance mode closed");
        }

        public static bool SwitchTo(uint instrumentId, int timeOut = 3000)
        {
            var sw = Stopwatch.StartNew();

            try
            {
                bool success = false;

                if (HSC.CurrentInstrument == 0)
                {
                    PerformActions.DoPerformAction(instrumentId);
                    success = WaitUntilChanged(() => HSC.CurrentInstrument == instrumentId, 100, timeOut);

                    if (!success)
                    {
                        SwitchInstrumentFailed();
                        ImGuiUtil.AddNotification(NotificationType.Error, $"Failed to equip instrument '{instrumentId}'.");
                        return false;
                    }

                    Thread.Sleep(200);
                    PluginLog.Information($"instrument switching succeed in {sw.Elapsed.TotalMilliseconds} ms");
                    ImGuiUtil.AddNotification(NotificationType.Success, $"Switched to {HSC.InstrumentStrings[instrumentId]}");
                    return true;
                }

                if (HSC.CurrentInstrument == instrumentId)
                    return true;

                if (HSC.guitarGroup.Contains(HSC.CurrentInstrument))
                {
                    if (HSC.guitarGroup.Contains((byte)instrumentId))
                    {
                        var tone = (int)instrumentId - HSC.guitarGroup[0];
                        playlib.GuitarSwitchTone(tone);

                        return true;
                    }
                }

                PerformActions.DoPerformAction(0);
                success = WaitUntilChanged(() => HSC.CurrentInstrument == 0, 100, 3000);

                if (!success)//dont try to equip if failed to unequip previous instrument (should prevent crashes)
                {
                    SwitchInstrumentFailed();
                    ImGuiUtil.AddNotification(NotificationType.Error, $"Failed to unequip current instrument..");
                    return false;
                }

                PerformActions.DoPerformAction(instrumentId);
                success = WaitUntilChanged(() => HSC.CurrentInstrument == instrumentId, 100, 3000);

                if (!success)
                {
                    SwitchInstrumentFailed();
                    ImGuiUtil.AddNotification(NotificationType.Error, $"Failed to equip instrument '{instrumentId}'.");
                    return false;
                }

                Thread.Sleep(200);
                PluginLog.Information($"instrument switching succeed in {sw.Elapsed.TotalMilliseconds} ms");
                ImGuiUtil.AddNotification(NotificationType.Success, $"Switched to {HSC.InstrumentStrings[instrumentId]}");

                return true;
            }

            catch (Exception e)
            {
                PluginLog.Error(e, $"instrument switching failed in {sw.Elapsed.TotalMilliseconds} ms");
                return false;
            }
            finally
            {

            }
        }

        public static bool SwitchInstrumentFromSong(bool force = false)
        {
            try
            {
                if (Settings.CharIndex == -1)
                {
                    ImGuiUtil.AddNotification(NotificationType.Error, $"Cannot switch instruments from HSC playlist for '{Settings.AppSettings.CurrentSong}'. Character config not loaded for '{Settings.CharName}'.");
                    return false;
                }

                if (!Configuration.config.SwitchInstrumentFromPlaylist && !force)
                    return HSC.CurrentInstrument != 0;

                if (Settings.SwitchInstrumentFailed)
                {
                    ImGuiUtil.AddNotification(NotificationType.Error, "Cannot switch instruments yet. Wait 3 seconds.");
                    return false;
                }

                PluginLog.Information($"Instrument switching from HSC playlist for '{Settings.AppSettings.CurrentSong}'");
                uint insId = GetInstrumentFromHscPlaylist();
                PluginLog.Information($"Switching to '{((Instrument)insId).ToString()}'");

                //if (api.PartyList.IsInParty())
                //    IpcHelpers.BroadcastSwitchInstruments();

                bool instrumentEquipped = SwitchTo(insId);

                if (!instrumentEquipped)
                {
                    PluginLog.Error($"Failed to equip instrument for '{Settings.AppSettings.CurrentSong}'.");
                    return false;
                }

                PerformanceHelpers.SendReadyCheckForPartyLeader();

                return true;
            }
            catch (Exception ex)
            {
                PluginLog.Error($"Error when switching instrument from HSC playlist. Message: {ex.Message}");
                return false;
            }

        }

        public static int GetGuitarTone(Track track) => (int)GetInstrumentFromName(track.EnsembleInstrument) - 24;

        public static bool HasGuitar(Track track)
        {
            var ins = GetInstrumentFromName(track.EnsembleInstrument);
            if (ins == Instrument.None)
                return false;
            return (int)ins >= (int)Instrument.ElectricGuitarOverdriven && (int)ins <= (int)Instrument.ElectricGuitarSpecial;
        }


        public static void SendReadyCheckForPartyLeader()
        {
            if (!Configuration.config.UseSendReadyCheck)
                return;

            if (!api.PartyList.IsInParty() || !api.PartyList.IsPartyLeader())
                return;

            Thread.Sleep(500);

            //wait for everyone to equip instruments first then send
            PluginLog.Information($"Sending ready check");
            playlib.BeginReadyCheck();
            playlib.ConfirmBeginReadyCheck();
            ImGuiUtil.AddNotification(NotificationType.Success, "$Ready check sent.");
        }

        public static bool WaitUntilChanged(Func<bool> condition, int delay = 100, int timeOut = 3000)
        {
            var sw = Stopwatch.StartNew();

            while (!condition())
            {
                if (condition())
                    return true;

                if ((int)sw.Elapsed.TotalMilliseconds >= timeOut)
                    return false;

                Thread.Sleep(delay);
            }

            return true;
        }

        private static uint GetInstrumentFromHscPlaylist()
        {
            try
            {
                if (Settings.CharIndex == -1)
                    return 0;

                if (Settings.CurrentSongSettings == null)
                    return 0;

                if (Settings.CurrentSongSettings.Tracks.IsNullOrEmpty())
                    return 0;

                var firstTrack = Settings.CurrentSongSettings.Tracks.Values.FirstOrDefault(
                    t => (Settings.AppSettings.TrackSettings.PopulateFromPlaylist ? t.EnsembleMember : t.AutofilledMember) == Settings.CharIndex);

                if (firstTrack == null)
                    return 0;

                return (uint)GetInstrumentFromName(GetAssignedInstrument(firstTrack));
            }

            catch (Exception e)
            {
                PluginLog.Error(e, $"Instrument switching from hsc playlist failed. {e.Message}");
                return 0;
            }
        }

        private static void SwitchInstrumentFailed()
        {
            Settings.SwitchInstrumentFailed = true;
            Task.Run(() =>
            {
                Thread.Sleep(Configuration.config.SwitchInstrumentDelay);
                Settings.SwitchInstrumentFailed = false;
            });
        }
    }
}
