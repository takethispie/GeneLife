using System.Reflection;
using GeneLife;
using GeneLifeConsole.CommandParser;
using Terminal.Gui;
using Arch.Bus;
using GeneLife.Common.Data;

var geneLifeSimulation = new GeneLifeSimulation(); 
geneLifeSimulation.Initialize();
var commandParser = new CommandParser(geneLifeSimulation);

bool GenelifeTick(MainLoop loop)
{
    geneLifeSimulation.Update(1);
    return true;
}

void ExecuteAndLog(string command, ListView logView)
{
    geneLifeSimulation.LogSystem.Logs.Add(commandParser.Parse(command));
    Application.MainLoop.Invoke(() =>
    {
        logView.SelectedItem = geneLifeSimulation.LogSystem.Logs.Count - 1;
        logView.SetNeedsDisplay();
    });
    
}

Application.Init();

var token = Application.MainLoop.AddTimeout(TimeSpan.FromSeconds(1), GenelifeTick);

var logs = new ListView
{
    X = 0, 
    Y = 0, 
    Width = Dim.Fill(), 
    Height = Dim.Fill(), 
    Visible = true,
    Source = new ListWrapper(geneLifeSimulation.LogSystem.Logs),
    SelectedItem = geneLifeSimulation.LogSystem.Logs.Count - 1,
    Enabled = false,
};

var menu = new MenuBar (new MenuBarItem [] {
    new("_File", new MenuItem [] {
        new("_Quit", "", () => { 
            Application.RequestStop ();
            Application.MainLoop.RemoveTimeout(token);
            geneLifeSimulation.Dispose();
        })
    }),
    new("_Create", new MenuItem[]
    {
        new("_Male NPC", "", () => ExecuteAndLog("create male", logs)),
        new("_Female NPC", "", () => ExecuteAndLog("create female", logs)),
    }),
    new("_Settings", new MenuItem[]
    {
        new("_Set ticks per day: 10", "", () => ExecuteAndLog("change ticksperday 10",logs)),
        new("_Set ticks per day: 100", "", () => ExecuteAndLog("change ticksperday 100",logs))
    })
});

var win = new Window ($"GeneLife Simulator version {Assembly.GetExecutingAssembly().GetName().Version}") {
    X = 0,
    Y = 1,
    Width = Dim.Fill (),
    Height = Dim.Fill ()
};

geneLifeSimulation.LogSystem.LogAdded += () => Application.MainLoop.Invoke(() =>
{
    logs.SelectedItem = geneLifeSimulation.LogSystem.Logs.Count - 1;
    logs.SetNeedsDisplay();
});

var tab = new TabView.Tab { View = logs, Text = "Logs"};
var tabView = new TabView { Visible = true,X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill() - 1 };
tabView.AddTab(tab, true);
var input = new TextField { Visible = true, X = 0, Y = Pos.Bottom(tabView) , Width = Dim.Percent(80), Height = 1 };

var executeCmd = new Button
{
    Visible = true, X = Pos.Right(input), Y = Pos.Bottom(tabView), Width = Dim.Percent(20), Height = 1, Text = "Execute"
};

executeCmd.Clicked += () =>
{
    if (string.IsNullOrEmpty(input.Text.ToString())) return;
    ExecuteAndLog(input.Text.ToString() ?? string.Empty, logs);
    input.Text = "";
};

win.Add(tabView);
win.Add(input);
win.Add(executeCmd);

Application.Top.Add (menu, win);
Application.Run ();
Application.Shutdown ();
