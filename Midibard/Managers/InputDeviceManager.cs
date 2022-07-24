﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Interface.Internal.Notifications;
using Dalamud.Logging;
using Dalamud.Plugin;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Multimedia;
using HSC.DalamudApi;
using HSC.Config;

namespace HSC;

/// <summary>
/// author: akira045 
/// </summary>
static class InputDeviceManager
{
    internal static readonly Thread ScanMidiDeviceThread =
        new Thread(() =>
            {
                PluginLog.Information("device scanning thread started.");

                while (ShouldScanMidiDeviceThread)
                {
                    try
                    {
                        Devices = InputDevice.GetAll().OrderBy(i => i.Name).ToArray();
                        var devicesNames = Devices.Select(i => i.DeviceName()).ToArray();

                        //PluginLog.Information(string.Join(", ", devicesNames));
                        //PluginLog.Information(Configuration.config.lastUsedMidiDeviceName);
                        if (CurrentInputDevice is not null)
                        {
                            if (!devicesNames.Contains(CurrentInputDevice.DeviceName()))
                            {
                                PluginLog.Debug("disposing disconnected device");
                                DisposeCurrentInputDevice();
                            }
                        }
                        else if (CurrentInputDevice is null)
                        {
                            if (Configuration.config.AutoRestoreListening)
                            {
                                if (devicesNames.Contains(Configuration.config.LastUsedMidiDeviceName))
                                {
                                    PluginLog.Warning($"try restoring midi device: \"{Configuration.config.LastUsedMidiDeviceName}\"");
                                    var newDevice = Devices?.FirstOrDefault(i =>
                                        i.Name == Configuration.config.LastUsedMidiDeviceName);
                                    if (newDevice != null)
                                    {
                                        SetDevice(newDevice);
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        PluginLog.Error(e, "error in midi device scanning thread");
                    }

                    Thread.Sleep(500);
                }
                PluginLog.Information("device scanning thread ended.");
            })
            { IsBackground = true, Priority = ThreadPriority.BelowNormal };

    internal static bool ShouldScanMidiDeviceThread = true;
    //internal static bool ShouldReloadMidiDevice = true;

    internal static bool IsListeningForEvents
    {
        get
        {
            var ret = false;
            try
            {
                ret = CurrentInputDevice?.IsListeningForEvents == true;
            }
            catch (Exception e)
            {
                PluginLog.Debug(e, "device maybe disposed.");
            }

            return ret;
        }
    }

    internal static string DeviceName(this InputDevice device)
    {
        return device?.Name ?? "None";
    }

    internal static InputDevice CurrentInputDevice { get; private set; }

    internal static string[] LastDevicesNames { get; private set; } = { };

    internal static InputDevice[] Devices { get; private set; } = { };

    internal static void SetDevice(InputDevice device)
    {
        DisposeCurrentInputDevice();
        Configuration.config.LastUsedMidiDeviceName = device?.DeviceName();
        if (device is null) return;

        try
        {
            CurrentInputDevice = device;
            CurrentInputDevice.SilentNoteOnPolicy = SilentNoteOnPolicy.NoteOff;
            CurrentInputDevice.EventReceived += InputDevice_EventReceived;
            CurrentInputDevice.StartEventsListening();
            //ImGuiUtil.AddNotification(NotificationType.Success,
            //    "Start event listening on \"{0}\".".Localize(CurrentInputDevice.Name),
            //    "Listening input device".Localize());
        }
        catch (Exception e)
        {
            //ImGuiUtil.AddNotification(NotificationType.Error,
            //    "\"{0}\" is not available now.\nPlease check log for further error information.".Localize(device.Name),
            //    "Cannot start listening Midi device".Localize());
            PluginLog.Error(e, "midi device is possibly being occupied.");
            DisposeCurrentInputDevice();
        }
    }

    internal static void DisposeCurrentInputDevice()
    {
        if (CurrentInputDevice == null) return;

        try
        {
            CurrentInputDevice.EventReceived -= InputDevice_EventReceived;
            CurrentInputDevice.Dispose();
            //ImGuiUtil.AddNotification(NotificationType.Info, $"Stop event listening on \"{CurrentInputDevice.Name}\"	.", "Midi device disconnected");
        }
        catch (Exception e)
        {
            PluginLog.Error(e, "error when disposing existing Input device");
        }
        finally
        {
            CurrentInputDevice?.Dispose();
            CurrentInputDevice = null;
        }
    }

    private static void InputDevice_EventReceived(object sender, MidiEventReceivedEventArgs e)
    {
        //PluginLog.Verbose($"[{sender}]{e.Event}");
        HSC.CurrentOutputDevice.SendEvent(e.Event);
    }
}