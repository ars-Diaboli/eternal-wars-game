using System.Text;
using EW.Scripts;
using EW.Scripts.DataTypes.Enums;
using EW.Scripts.Messaging;
using EW.Scripts.Players;
using Godot;

public partial class ResourcePanel : PanelContainer,
    IHandleEvent<WorldMapResourceUiRefreshRequested>,
    IHandleEvent<TurnStart>
{
    [Export] public Label ResourceLabel { get; set; }
    [Export] public TurnManager TurnManager { get; set; }

    public override void _Ready()
    {
        ResolveReferences();
        UpdateResourceLabel();
    }

    public void Handle(WorldMapResourceUiRefreshRequested eventData)
    {
        UpdateResourceLabel();
    }

    public void Handle(TurnStart eventData)
    {
        UpdateResourceLabel();
    }

    private void UpdateResourceLabel()
    {
        ResolveReferences();
        if (ResourceLabel == null)
        {
            GD.PushWarning($"{Name}: ResourceLabel is not assigned and could not be resolved.");
            return;
        }

        GamePlayer observedPlayer = ResolveObservedPlayer();
        ResourcesCollection economy = observedPlayer?.Economy.Resources;
        if (economy == null)
        {
            ResourceLabel.Text = "Player Resources\n-";
            return;
        }

        StringBuilder builder = new();
        builder.AppendLine($"{observedPlayer} Resources");

        foreach (ResourceType resourceType in System.Enum.GetValues<ResourceType>())
        {
            builder.AppendLine($"{resourceType}: {economy[resourceType]}");
        }

        ResourceLabel.Text = builder.ToString().TrimEnd();
    }

    private GamePlayer ResolveObservedPlayer()
    {
        return TurnManager?.LocalPlayer ?? TurnManager?.CurrentPlayer;
    }

    private void ResolveReferences()
    {
        Node sceneRoot = GetTree().CurrentScene ?? GetParent();
        TurnManager ??= sceneRoot?.GetNodeOrNull<TurnManager>("TurnManager");
        ResourceLabel ??= GetNodeOrNull<Label>("MarginContainer/ResourceLabel");
    }
}
