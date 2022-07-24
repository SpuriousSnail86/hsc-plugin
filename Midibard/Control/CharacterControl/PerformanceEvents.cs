﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static HSC.HSC;

namespace HSC.Control.CharacterControl;

/// <summary>
/// author: akira045/Ori
/// </summary>
class PerformanceEvents
{
    private PerformanceEvents()
    {

    }

    public static PerformanceEvents Instance { get; } = new PerformanceEvents();

    private void EnteringPerformance()
    {
        //if (Configuration.config.AutoOpenPlayerWhenPerforming)
        //    if (!SwitchInstrument.SwitchingInstrument)
        //        Ui.Open();

        //_backgroundFrameLimit = AgentConfigSystem.BackgroundFrameLimit;
        //AgentConfigSystem.BackgroundFrameLimit = false;
        //AgentConfigSystem.ApplyGraphicSettings();
    }

    private void ExitingPerformance()
    {
        //if (Configuration.config.AutoOpenPlayerWhenPerforming)
        //    if (!SwitchInstrument.SwitchingInstrument)
        //        Ui.Close();

        //if (_backgroundFrameLimit is { } b && AgentConfigSystem.BackgroundFrameLimit != b)
        //{
        //    AgentConfigSystem.BackgroundFrameLimit = b;
        //    AgentConfigSystem.ApplyGraphicSettings();
        //}
    }

    private bool inPerformanceMode;
    private bool? _backgroundFrameLimit;

    public bool InPerformanceMode
    {
        set
        {
            if (value && !inPerformanceMode)
            {
                EnteringPerformance();
            }

            if (!value && inPerformanceMode)
            {
                ExitingPerformance();
            }

            inPerformanceMode = value;
        }
    }
}