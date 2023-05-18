using GeneLife;
using Terminal.Gui;

namespace GeneLifeConsole.Views;

public class MenuBuilder
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
            }),
            new("_Settings", new MenuItem[]
            {
                new("_Set ticks per day: 10", "", () => executeAndLog("change ticksperday 10")),
                new("_Set ticks per day: 100", "", () => executeAndLog("change ticksperday 100"))
            })
        });
    }
}