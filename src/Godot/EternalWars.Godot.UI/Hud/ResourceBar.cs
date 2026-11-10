using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Godot.Shared.Events;
using Godot;

namespace EternalWars.Godot.UI.Hud;

/// <summary>
/// Shows the resources of the player whose turn it is (hot-seat friendly): each resource's icon and amount, its name in the tooltip
/// (the name before the amount when the icon file is missing).
/// </summary>
public partial class ResourceBar : HBoxContainer
{
    private readonly Dictionary<ResourceType, Label> _labels = [];
    private readonly Dictionary<ResourceType, bool> _named = []; // no icon file: the label names the resource
    private PlayerId _shown;

    public void Bind(GameSession session)
    {
        AddThemeConstantOverride("separation", UiSpacing.Section);
        foreach (var type in Enum.GetValues<ResourceType>())
        {
            var item = new HBoxContainer { TooltipText = type.ToString() };
            item.AddThemeConstantOverride("separation", UiSpacing.Cell);
            var icon = ResourceIcons.Get(type);
            if (icon is not null)
            {
                item.AddChild(new TextureRect { Texture = icon, StretchMode = TextureRect.StretchModeEnum.KeepCentered, MouseFilter = MouseFilterEnum.Pass });
            }

            var label = new Label { MouseFilter = MouseFilterEnum.Pass };
            _labels[type] = label;
            _named[type] = icon is null;
            item.AddChild(label);
            AddChild(item);
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
            label.Text = _named[type] ? $"{type}: {resources[type]}" : resources[type].ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
