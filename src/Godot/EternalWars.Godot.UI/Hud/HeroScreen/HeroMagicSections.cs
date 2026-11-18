using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Info;
using EternalWars.Core.Domain.Magic;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Events;
using EternalWars.Godot.Shared.Controls;
using EternalWars.Godot.Shared.Events;
using Godot;

namespace EternalWars.Godot.UI.Hud.HeroScreen;

/// <summary>The hero's war machines, each its icon with the machine's name as the tooltip (the name when it has no icon).</summary>
internal sealed class WarMachinesSection(HeroPanel panel) : HeroSection(panel)
{
    public HFlowContainer Row { get; } = new();

    protected override Node Anchor => Row;

    public override void Show(Hero hero)
    {
        Row.ClearChildren();
        var state = State;
        Row.AddChild(new Label { Text = hero.WarMachines.Count == 0 ? "War machines: none" : "War machines:" });
        foreach (var slot in hero.WarMachines.Order())
        {
            var name = TownRules.WarMachineName(state, hero, slot);
            Row.AddChild(HeroIcons.Machine(state, hero, slot, HeroIcons.SkillSize * 2) is { } texture
                ? new TextureRect
                {
                    Texture = texture,
                    TooltipText = name,
                    StretchMode = TextureRect.StretchModeEnum.KeepCentered,
                    CustomMinimumSize = new Vector2(HeroIcons.SkillSize * 2, HeroIcons.SkillSize * 2),
                }
                : new Label { Text = name });
        }
    }

    protected override void Subscribe(RefreshScheduler refresh, IEventSubscriber events) => refresh.On<WarMachineBought>(events);
}

/// <summary>The spell book: one button per known spell (a right click opens its window), and how many the worn artifacts lend.</summary>
internal sealed class SpellBookSection(HeroPanel panel) : HeroSection(panel)
{
    public HFlowContainer Row { get; } = new();

    protected override Node Anchor => Row;

    public override void Show(Hero hero)
    {
        Row.ClearChildren();
        var text = SpellsText(hero, State.Spells);
        Row.AddChild(hero.HasSpellBook ? UiIcons.IconLabel(UiIcon.Spells, text) : new Label { Text = text });
        if (!hero.HasSpellBook)
        {
            return;
        }

        foreach (var (spell, rank) in MagicRules.Known(hero, State.Spells))
        {
            var id = spell.Id;
            Row.AddChild(new Button { Text = MagicRules.NameAtRank(spell, rank), Flat = true, TooltipText = spell.Notes }
                .WithInfo(detailed => InfoSheets.Spell(Panel.Session.State, id, rank, detailed, hero)));
        }
    }

    /// <summary>The spells learned, the book, the artifacts that lend spells, and the skills that raise their ranks.</summary>
    protected override void Subscribe(RefreshScheduler refresh, IEventSubscriber events) =>
        OnSpells(OnArtifacts(refresh, events), events)
            .On<HeroLeveledUp>(events)
            .On<PandorasBoxOpened>(events);

    /// <summary>The learned spells by name, and how many more the worn artifacts teach (the Unwritten Codex, ADR-111; scrolls, ADR-136).</summary>
    private static string SpellsText(Hero hero, SpellCatalog spells)
    {
        if (!hero.HasSpellBook)
        {
            return "No spell book.";
        }

        var lent = MagicRules.Known(hero, spells).Count() - hero.Spells.Count;
        var known = hero.Spells.Count == 0 && lent == 0 ? "Spell book: empty." : "Spells:";
        return lent > 0 ? $"{known} ({lent} lent while its artifacts are worn)" : known;
    }
}

/// <summary>One button per adventure spell the hero knows (ADR-120), disabled with the reason while it cannot be cast.</summary>
internal sealed class AdventureSpellsSection(HeroPanel panel) : HeroSection(panel)
{
    public HFlowContainer Row { get; } = new();

    protected override Node Anchor => Row;

    public override void Show(Hero hero)
    {
        Row.ClearChildren();
        var buttons = AdventureSpellButtons.For(
            State,
            hero,
            cast: spell => Run(new CastAdventureSpellCommand(State.CurrentPlayer.Id, hero.Id, spell)),
            arm: spell => Panel.ArmAdventureSpell(hero.Id, spell),
            ledger: () => Panel.ReopenLedger(hero.Id),
            eavesdrop: town => Panel.ReopenEavesdrop(hero.Id, town));
        foreach (var button in buttons)
        {
            Row.AddChild(button);
        }

        if (Row.GetChildCount() == 0)
        {
            Row.AddChild(new Label { Text = "none" });
        }
    }

    /// <summary>Mana, movement, statuses, the spells known and the artifacts decide what can be cast.</summary>
    protected override void Subscribe(RefreshScheduler refresh, IEventSubscriber events) =>
        OnSpells(OnHeroState(OnArtifacts(refresh, events), events), events);
}
