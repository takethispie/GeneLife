using Arch.Core;
using Arch.Core.Extensions;
using GeneLife;
using GeneLife.Core.Components;
using GeneLife.Core.Components.Characters;
using GeneLife.Core.Items;
using GeneLife.Core.Utils;
using GeneLife.Genetic;
using GeneLife.Sibyl.Components;
using Terminal.Gui;

namespace GeneLifeConsole.Views;

public sealed class NPCView : View
{
    public ListView EntitiesList { get; }
    public FrameView Details { get; }
    private Label _fullNameLabel, _hungerLabel, _damageLabel, _staminaLabel, _thirstLabel;
    private Label _eyeColor, _hairColor, _sex, _age, _handedness, _heightPotential, _intelligence, _behaviorPropension, _morphotype;
    private Label _inventoryLabel;
    private Button _foodBtn, _drinkBtn;
    private ListView _knowledgeList, _inventoryList;
    private GeneLifeSimulation Simulation;
    private readonly Action<string> _executeAndLog;
    private Action<ListViewItemEventArgs> OnSelectionChange;
    private Entity _selected;
    
    public NPCView(GeneLifeSimulation simulation, Action<string> ExecuteAndLog)
    {
        X = 0;
        Y = 0;
        Width = Dim.Fill();
        Height = Dim.Fill();
        Visible = true;
        Simulation = simulation;
        _executeAndLog = ExecuteAndLog;
        EntitiesList = new ListView
        {
            X = 0, 
            Y = 0, 
            Width = Dim.Percent(30), 
            Height = Dim.Fill() - 1, 
            Visible = true,
            Source = new ListWrapper(simulation.GetLivingEntities().Select(x => x.Id.ToString()).ToArray()),
            SelectedItem = simulation.GetLivingEntities().Length - 1
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
        _fullNameLabel = new Label { X = Pos.Left(Details), Y = Pos.Top(Details), Height = 1, Width = Dim.Fill(), Visible = true };
        _hungerLabel = new Label { X = 2, Y = 3, Width = Dim.Percent(50), Height = 1, Visible = true };
        _thirstLabel = new Label { X = 2, Y = 4, Width = Dim.Percent(50), Height = 1, Visible = true };
        _staminaLabel = new Label { X = 2, Y = 5, Width = Dim.Percent(50), Height = 1, Visible = true };
        _damageLabel = new Label { X = 2, Y = 6, Width = Dim.Percent(50), Height = 1, Visible = true };
        Details.Add(_fullNameLabel);
        Details.Add(_hungerLabel);
        Details.Add(_thirstLabel);
        Details.Add(_staminaLabel);
        Details.Add(_damageLabel);
        
        _eyeColor = new Label { X = Pos.Percent(30), Y = 3, Width = Dim.Fill(), Height = 1, Visible = true };
        _age = new Label { X = Pos.Percent(30), Y = 4, Width = Dim.Fill(), Height = 1, Visible = true };
        _sex = new Label { X = Pos.Percent(30), Y = 5, Width = Dim.Fill(), Height = 1, Visible = true };
        _hairColor = new Label { X = Pos.Percent(30), Y = 6, Width = Dim.Fill(), Height = 1, Visible = true };
        _handedness = new Label { X = Pos.Percent(30), Y = 7, Width = Dim.Fill(), Height = 1, Visible = true };
        _heightPotential = new Label { X = Pos.Percent(30), Y = 8, Width = Dim.Fill(), Height = 1, Visible = true };
        _morphotype = new Label { X = Pos.Percent(30), Y = 9, Width = Dim.Fill(), Height = 1, Visible = true };
        _intelligence = new Label { X = Pos.Percent(30), Y = 10, Width = Dim.Fill(), Height = 1, Visible = true };
        _behaviorPropension = new Label { X = Pos.Percent(30), Y = 11, Width = Dim.Fill(), Height = 1, Visible = true };
        Details.Add(_eyeColor);
        Details.Add(_age);
        Details.Add(_sex);
        Details.Add(_hairColor);
        Details.Add(_handedness);
        Details.Add(_heightPotential);
        Details.Add(_morphotype);
        Details.Add(_intelligence);
        Details.Add(_behaviorPropension);
        
        _knowledgeList = new ListView
        {
            X = 0, 
            Y = 8, 
            Width = Dim.Percent(20), 
            Height = Dim.Fill() - 1, 
            Visible = true,
            Enabled = false
        };
        _inventoryLabel = new Label { X = Pos.Percent(30), Y = 13, Width = Dim.Fill(), Height = 1, Visible = true, Text = "Inventory:"};
        _inventoryList = new ListView
        {
            X = Pos.Percent(30), 
            Y = 14, 
            Width = Dim.Percent(30),
            Height = Dim.Fill() - 1, 
            Visible = true,
            Enabled = false,
        };
        Details.Add(_knowledgeList);
        Details.Add(_inventoryLabel);
        Details.Add(_inventoryList);
        Add(EntitiesList);
        Add(Details);

        _foodBtn = new Button
        {
            X = Pos.Percent(70),
            Y = 0,
            Width = Dim.Fill(),
            Height = 1,
            Text = "Give Food",
            Visible = true
        };
        _foodBtn.Clicked += () =>
        {
            if (EntitiesList.Source.Count <= 0 || EntitiesList.SelectedItem < 0) return;
            var identity = _selected.Get<Identity>();
            ExecuteAndLog($"give 1 {identity.FirstName},{identity.LastName}");
            LoadEntityData();
            EntitiesList.SetFocus();
        };  
        
        _drinkBtn = new Button
        {
            X = Pos.Percent(70),
            Y = 1,
            Width = Dim.Fill(),
            Height = 1,
            Text = "Give Drink",
            Visible = true
        };
        _drinkBtn.Clicked += () =>
        {
            if (EntitiesList.Source.Count <= 0 || EntitiesList.SelectedItem < 0) return;
            var identity = _selected.Get<Identity>();
            ExecuteAndLog($"give 2 {identity.FirstName},{identity.LastName}");
            LoadEntityData();
            EntitiesList.SetFocus();
        };  
        
        Details.Add(_foodBtn);
        Details.Add(_drinkBtn);
        simulation.UiHookSystem.EntityUpdated += OnHookEntityUpdate;
    }

    public void Load(List<Entity> entities) => EntitiesList.SetSource(entities.Select(x => x.Id).ToArray());

    private void LoadEntityData()
    {
        _fullNameLabel.Text = _selected.Get<Identity>().FullName();
        var living = _selected.Get<Living>();
        _hungerLabel.Text = $"Hunger: {living.Hunger.ToString()}/10";
        _thirstLabel.Text = $"Thirst: {living.Thirst.ToString()}/20";
        _staminaLabel.Text = $"Stamina: {living.Stamina.ToString()}/10";
        _damageLabel.Text = $"Damage: {living.Damage.ToString()}";
        if (_selected.Has<Genome>())
        {
            var genome = _selected.Get<Genome>();
            _age.Text = $"Age: {genome.Age.ToString()}";
            _eyeColor.Text = $"Eyes Color: {genome.EyeColor}";
            _sex.Text = $"Sex: {genome.Sex}";
            _hairColor.Text = $"Hair Color: {genome.HairColor}";
            _handedness.Text = $"Handedness: {genome.Handedness}";
            _heightPotential.Text = $"Height: {genome.HeightPotential}";
            _morphotype.Text = $"Morphotype: {genome.Morphotype}";
            _intelligence.Text = $"Intelligence Type: {genome.Intelligence}";
            _behaviorPropension.Text = $"Behavior Type: {genome.BehaviorPropension}";
        }

        if (_selected.Has<KnowledgeList>())
        {
            var knowledgeList = _selected.Get<KnowledgeList>();
            _knowledgeList.Source = new ListWrapper(knowledgeList.KnownCategories);
        }

        if (_selected.Has<Inventory>())
        {
            var inventory = _selected.Get<Inventory>();
            _inventoryList.Source = new ListWrapper(
                inventory.items
                    .Where(x => x.Type != ItemType.None)
                    .Select(x => x.Description())
                    .ToArray()
            );
        }
        EntitiesList.SetNeedsDisplay();
    }

    private void SetDetails(ListViewItemEventArgs args)
    {
        Simulation.removeLivingEntityUpdateHook(_selected.Id);
        _selected = Simulation.GetLivingEntities().First(x => x.Id == (int)args.Value);
        if (!_selected.Has<Identity>()) return;
        Simulation.SetLivingEntityUpdateHook(_selected.Id);
        LoadEntityData();
    }

    public void OnHookEntityUpdate(Entity entity)
    {
        if (_selected.Id == entity.Id) _selected = entity;
        LoadEntityData();
    }
}