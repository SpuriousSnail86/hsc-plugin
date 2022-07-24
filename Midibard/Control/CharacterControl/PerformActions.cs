using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Dalamud.Logging;
using HSC.Memory;

namespace HSC.Control.CharacterControl;

/// <summary>
/// author: akira045/Ori
/// </summary>
static class PerformActions
{
    internal delegate void DoPerformActionDelegate(IntPtr performInfoPtr, uint instrumentId, int a3 = 0);
    private static DoPerformActionDelegate doPerformAction { get; } = Marshal.GetDelegateForFunctionPointer<DoPerformActionDelegate>(Offsets.DoPerformAction);
    public static void DoPerformAction(uint instrumentId)
    {
        PluginLog.Information($"[DoPerformAction] instrumentId: {instrumentId}");
        doPerformAction(Offsets.PerformanceStructPtr, instrumentId);
    }
}