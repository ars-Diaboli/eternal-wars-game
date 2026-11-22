using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Events;
using EternalWars.Godot.Shared.Controls;
using EternalWars.Godot.Shared.Events;
using Godot;

namespace EternalWars.Godot.UI.Hud.TownScreen;

/// <summary>The header: the town's crest, name and daily income, and the Marketplace and Thieves' Guild buttons while the town has them.</summary>
internal sealed class TownHeaderSection(ITownSectionHost panel) : TownSection(panel)
{
    public Label Title { get; } = new() { VerticalAlignment = VerticalAlignment.Center };

    public TextureRect Crest { get; } = new()
    {
        StretchMode = TextureRect.StretchModeEnum.KeepCentered,
        MouseFilter = Control.MouseFilterEnum.Pass,
        CustomMinimumSize = new Vector2(Heraldry.TitleSize, Heraldry.TitleSize),
    };

    public HBoxContainer Income { get; } = new();

    public Button Trade { get; } = new Button { Text = "Marketplace", TooltipText = "Trade gold against resources" }.WithResourceIcon(ResourceType.Gold);

    public Button Guild { get; } = new Button { Text = "Thieves' guild", TooltipText = "How you rank against the other players; more taverns show more" }
        .WithIcon(UiIcon.ThievesGuild);

    protected override Node Anchor => Title;

    public override void Show(Town town)
    {
        var state = State;
        Crest.Texture = Heraldry.Crest(town.TownType, Heraldry.TitleSize);
        Crest.TooltipText = town.TownType;
        Crest.Visible = Crest.Texture is not null;
        Title.Text = $"{town.Name} ({town.TownType}), {state.CurrentPlayer.Name}   income";
        Income.ClearChildren();
        Income.AddChild(ResourceIcons.CostRow(TownRules.DailyIncome(state, town), " a day"));
        Trade.Visible = town.Level(BuiltInBuildings.Marketplace) > 0;
        Guild.Visible = town.Level(BuiltInBuildings.Tavern) > 0;
    }

    /// <summary>Only the buildings, the governor and the turn change it: the screen rebuilds it then.</summary>
    protected override void Subscribe(RefreshScheduler refresh, IEventSubscriber events)
    {
    }
}
