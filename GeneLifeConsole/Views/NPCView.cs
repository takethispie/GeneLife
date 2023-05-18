using Arch.Core;
using Arch.Core.Extensions;
using GeneLife;
using GeneLife.Common.Components.Utils;
using GeneLife.Utils;
using Terminal.Gui;

namespace GeneLifeConsole.Views;

public class NPCView : View
{
    public ListView EntitiesList { get; init; }
    public FrameView Details { get; init; }
    private Label _fullNameLabel;
    private GeneLifeSimulation Simulation;
    private Action<ListViewItemEventArgs> OnSelectionChange;
    
    public NPCView(GeneLifeSimulation simulation)
    {
        X = 0;
        Y = 0;
        Width = Dim.Fill();
        Height = Dim.Fill();
        Visible = true;
        Simulation = simulation;
        EntitiesList = new ListView
        {
            X = 0, 
            Y = 0, 
            Width = Dim.Percent(30), 
            Height = Dim.Fill(), 
            Visible = true,
            Source = new ListWrapper(simulation.Entities.Select(x => x.Id.ToString()).ToArray()),
            SelectedItem = simulation.Entities.Count - 1,
        };
        EntitiesList.SelectedItemChanged += SetDetails;
        Details = new FrameView
        {
            Visible = true,
            X = Pos.Right(EntitiesList),
            Y = 0,
            Width = Dim.Percent(70),
            Height = Dim.Fill()
        };
        _fullNameLabel = new Label { X = Pos.Left(Details), Y = Pos.Top(Details), Height = 1, Width = Dim.Fill(), Visible = true, Text = "test"};
        Details.Add(_fullNameLabel);
        Add(EntitiesList);
        Add(Details);
    }

    public void Load(List<Entity> entities) => EntitiesList.SetSource(entities.Select(x => x.Id).ToArray());


    private void SetDetails(ListViewItemEventArgs args)
    {
        _fullNameLabel.Text = Simulation.Entities.First(x => x.Id == (int)args.Value).Get<Identity>().FullName();
        EntitiesList.SetNeedsDisplay();
    }
}