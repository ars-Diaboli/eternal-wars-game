using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Events;
using EternalWars.Godot.Shared.Events;
using Godot;

namespace EternalWars.Godot.UI.Hud.HeroScreen;

/// <summary>The header: the hero's crest, name, level, class and faction.</summary>
internal sealed class HeroHeaderSection(HeroPanel panel) : HeroSection(panel)
{
    public Label Title { get; } = new() { VerticalAlignment = VerticalAlignment.Center };

    public TextureRect Crest { get; } = new()
    {
        StretchMode = TextureRect.StretchModeEnum.KeepCentered,
        MouseFilter = Control.MouseFilterEnum.Pass,
        CustomMinimumSize = new Vector2(Heraldry.TitleSize, Heraldry.TitleSize),
    };

    protected override Node Anchor => Title;

    public override void Show(Hero hero)
    {
        var faction = hero.Definition is { } d ? State.HeroRoster.Get(d).Town : null;
        Title.Text = $"{hero.Name}, level {hero.Level} {hero.Class}" + (faction is null ? "" : $" ({faction})");
        Crest.Texture = Heraldry.Crest(faction, Heraldry.TitleSize);
        Crest.TooltipText = faction ?? "";
        Crest.Visible = Crest.Texture is not null;
    }

    protected override void Subscribe(RefreshScheduler refresh, IEventSubscriber events) => refresh.On<HeroLeveledUp>(events);
}
