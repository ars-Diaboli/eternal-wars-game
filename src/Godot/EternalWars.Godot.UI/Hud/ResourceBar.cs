using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Godot.Shared.Events;
using Godot;

namespace EternalWars.Godot.UI.Hud;

/// <summary>
/// Shows the resources of the player whose turn it is (hot-seat friendly; in a LAN game this screen's player's in another's turn, ADR-192): each resource's icon and amount, its name in the tooltip
/// (the name before the amount when the icon file is missing). Large amounts are shortened ("321.7m", ADR-186); the tooltip then gives them in full.
/// </summary>
public partial class ResourceBar : HBoxContainer
{
    private readonly Dictionary<ResourceType, Label> _labels = [];
    private readonly Dictionary<ResourceType, Control> _items = [];
    private readonly Dictionary<ResourceType, bool> _named = []; // no icon file: the label names the resource
    private PlayerId _shown;

    public void Bind(GameSession session, Func<PlayerId>? listed = null)
    {
        listed ??= () => session.State.CurrentPlayer.Id;
        AddThemeConstantOverride("separation", UiSpacing.Section);
        foreach (var type in Enum.GetValues<ResourceType>())
        {
            var item = new HBoxContainer { TooltipText = type.ToString() };
            item.AddThemeConstantOverride("separation", UiSpacing.Cell);
            // What the player holds, not a price: gold shows the pile (ResourceIcons.Balance), the others their usual icon.
            var icon = type == ResourceType.Gold ? ResourceIcons.Treasury(ResourceIcons.TextSize) ?? ResourceIcons.Get(type) : ResourceIcons.Get(type);
            if (icon is not null)
            {
                item.AddChild(new TextureRect { Texture = icon, StretchMode = TextureRect.StretchModeEnum.KeepCentered, MouseFilter = MouseFilterEnum.Pass });
            }

            var label = new Label { MouseFilter = MouseFilterEnum.Pass };
            _labels[type] = label;
            _named[type] = icon is null;
            _items[type] = item;
            item.AddChild(label);
            AddChild(item);
        }

        _shown = listed();
        Display(session.State.GetPlayer(_shown).Resources);

        this.SubscribeWhileInTree<TurnStarted>(session.Events, _ =>
        {
            _shown = listed();
            Display(session.State.GetPlayer(_shown).Resources);
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
            var amount = ResourceAmountText.Short(type, resources[type]);
            label.Text = _named[type] ? $"{type}: {amount}" : amount;
            _items[type].TooltipText = ResourceAmountText.IsShortened(type, resources[type]) ? $"{type}: {ResourceAmountText.Full(resources[type])}" : type.ToString();
        }
    }
}
