// See https://aka.ms/new-console-template for more information

using System.Net.Mime;
using System.Reflection;
using GeneLife;
using GeneLifeConsole.CommandParser;

var geneLifeSimulation = new GeneLifeSimulation(); 
geneLifeSimulation.Initialize();
var commandParser = new CommandParser(geneLifeSimulation);

async Task RunInBackground(TimeSpan timeSpan, Action action, CancellationToken token)
{
    var periodicTimer = new PeriodicTimer(timeSpan);
    if (token.IsCancellationRequested) return;
    while (await periodicTimer.WaitForNextTickAsync(token))
    {
        action();
    }
}

void GenelifeTick()
{
    geneLifeSimulation?.Update(1);
}

Console.WriteLine($"GeneLife Population simulator version {Assembly.GetExecutingAssembly().GetName().Version}");
Console.WriteLine("Starting simulation...");
var cancellation = new CancellationTokenSource();
var token = cancellation.Token;
var task = RunInBackground(TimeSpan.FromSeconds(1), GenelifeTick, token);
Console.WriteLine("Simulation started");

var command = "";
while (command.ToLower().Trim() != "stop")
{
    command = Console.ReadLine();
    if (command != null) Console.WriteLine(commandParser.Parse(command));
    Thread.Sleep(TimeSpan.FromMilliseconds(100));
}
cancellation.Cancel();
task.Dispose();
Console.WriteLine("stopped");
