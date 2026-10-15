using System.Text;
using EW.Scripts;
using EW.Scripts.DataTypes.Enums;
using EW.Scripts.Messaging;
using EW.Scripts.Players;
using Godot;

namespace EW.Scripts.Interface;

public partial class StatusBar : PanelContainer,
    IHandleEvent<WorldMapResourceUiRefreshRequested>,
    IHandleEvent<TurnStart>
{
    [Export] public Label StatusLabel { get; set; }
    [Export] public TurnManager TurnManager { get; set; }

    public override void _Ready()
    {
        ResolveReferences();
        UpdateResources();
    }

    public void Handle(WorldMapResourceUiRefreshRequested eventData)
    {
        UpdateResources();
    }

    public void Handle(TurnStart eventData)
    {
        UpdateResources();
    }

    private void UpdateResources()
    {
        ResolveReferences();
        if (StatusLabel == null)
        {
            GD.PushWarning($"{Name}: StatusLabel is not assigned and could not be resolved.");
            return;
        }

        GamePlayer observedPlayer = ResolveObservedPlayer();
        ResourcesCollection resources = observedPlayer?.Economy.Resources;
        if (resources == null)
        {
            StatusLabel.Text = "Resources: -";
            return;
        }

        StringBuilder builder = new();
        builder.Append("Resources: ");

        bool isFirst = true;
        foreach (ResourceType resourceType in System.Enum.GetValues<ResourceType>())
        {
            if (!isFirst)
            {
                builder.Append("    ");
            }

            builder.Append($"{resourceType}: {resources[resourceType]}");
            isFirst = false;
        }

        StatusLabel.Text = builder.ToString();
    }

    private GamePlayer ResolveObservedPlayer()
    {
        return TurnManager?.LocalPlayer ?? TurnManager?.CurrentPlayer;
    }

    private void ResolveReferences()
    {
        Node sceneRoot = GetTree().CurrentScene ?? GetParent();
        TurnManager ??= sceneRoot?.GetNodeOrNull<TurnManager>("TurnManager");
        StatusLabel ??= GetNodeOrNull<Label>("MarginContainer/StatusLabel");
    }
}
