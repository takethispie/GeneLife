﻿using GeneLife;
using Terminal.Gui;

namespace GeneLifeConsole.Views;

public sealed class MenuBuilder
{
    public static View Build(GeneLifeSimulation simulation, Action<string> executeAndLog, object token)
    {
        return new MenuBar (new MenuBarItem [] {
            new("_File", new MenuItem [] {
                new("_Quit", "", () => { 
                    Application.RequestStop ();
                    Application.MainLoop.RemoveTimeout(token);
                    simulation.Dispose();
                })
            }),
            new("_Create", new MenuItem[]
            {
                new("_Male NPC", "", () => executeAndLog("create male")),
                new("_Female NPC", "", () => executeAndLog("create female")),
                new("_Small City", "", () => executeAndLog("create city small")),
            }),
            new("_Settings", new MenuItem[]
            {
                new("_Set ticks per day: 1", "", () => executeAndLog("change ticksperday 1")),
                new("_Set ticks per day: 3", "", () => executeAndLog("change ticksperday 3")),
                new("_Set ticks per day: 5", "", () => executeAndLog("change ticksperday 5")),
                new("_Set ticks per day: 10", "", () => executeAndLog("change ticksperday 10")),
                new("_Set ticks per day: 100", "", () => executeAndLog("change ticksperday 100"))
            })
        });
    }
}