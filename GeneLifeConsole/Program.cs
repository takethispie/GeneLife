using System.Reflection;
using GeneLife;
using GeneLifeConsole.CommandParser;
using Terminal.Gui;
using Arch.Bus;
using GeneLifeConsole.Views;

var geneLifeSimulation = new GeneLifeSimulation(); 
geneLifeSimulation.Initialize();
var commandParser = new CommandParser(geneLifeSimulation);

bool GenelifeTick(MainLoop loop)
{
    geneLifeSimulation.Update(1);
    return true;
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

void ExecuteAndLog(string command)
{
    geneLifeSimulation.LogSystem.Logs.Add(commandParser.Parse(command));
    Application.MainLoop.Invoke(() =>
    {
        logs.MoveEnd();
        logs.SetNeedsDisplay();
    });
}

var menu = MenuBuilder.Build(geneLifeSimulation, ExecuteAndLog, token);

var win = new Window ($"GeneLife Simulator version {Assembly.GetExecutingAssembly().GetName().Version}") {
    X = 0,
    Y = 1,
    Width = Dim.Fill (),
    Height = Dim.Fill ()
};


var npcView = new NPCView(geneLifeSimulation, ExecuteAndLog);
npcView.DrawContent += _ =>
{
    npcView.Load(geneLifeSimulation.GetAllLivingNPC());
};

var logTab = new TabView.Tab { View = logs, Text = "Logs"};
var npcListTab = new TabView.Tab { View = npcView,Text = "NPCs" };

var tabView = new TabView { Visible = true,X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill() - 1 };
tabView.AddTab(logTab, true);
tabView.AddTab(npcListTab, false);

geneLifeSimulation.LogSystem.LogAdded += () => Application.MainLoop.Invoke(() =>
{
    logs.MoveEnd();
    logs.SetNeedsDisplay();
});

#region Command input
var input = new TextField { Visible = true, X = 1, Y = Pos.Bottom(tabView) , Width = Dim.Percent(80), Height = 1 };
var executeCmd = new Button
{
    Visible = true, X = Pos.Right(input), Y = Pos.Bottom(tabView), Width = Dim.Percent(20), Height = 1, Text = "Execute"
};
executeCmd.Clicked += () =>
{
    if (string.IsNullOrEmpty(input.Text.ToString())) return;
    ExecuteAndLog(input.Text.ToString() ?? string.Empty);
    input.Text = "";
};
#endregion

win.Add(tabView);
win.Add(input);
win.Add(executeCmd);

Application.Top.Add (menu, win);
Application.Run();
Application.Shutdown();
