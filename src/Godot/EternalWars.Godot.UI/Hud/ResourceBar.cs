using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Godot.Shared.Events;
using Godot;

namespace EternalWars.Godot.UI.Hud;

/// <summary>Shows the resources of the player whose turn it is (hot-seat friendly).</summary>
public partial class ResourceBar : HBoxContainer
{
    private readonly Dictionary<ResourceType, Label> _labels = [];
    private PlayerId _shown;

    public void Bind(GameSession session)
    {
        AddThemeConstantOverride("separation", 16);
        foreach (var type in Enum.GetValues<ResourceType>())
        {
            var label = new Label();
            _labels[type] = label;
            AddChild(label);
        }

        _shown = session.State.CurrentPlayer.Id;
        Display(session.State.CurrentPlayer.Resources);

        this.SubscribeWhileInTree<TurnStarted>(session.Events, e =>
        {
            _shown = e.Player;
            Display(session.State.GetPlayer(e.Player).Resources);
        });
        this.SubscribeWhileInTree<ResourcesChanged>(session.Events, e =>
        {
            if (e.Player == _shown)
            {
                Display(e.Current);
            }
        });
    }

    private void Display(ResourceSet resources)
    {
        foreach (var (type, label) in _labels)
        {
            label.Text = $"{type}: {resources[type]}";
        }
    }
}
