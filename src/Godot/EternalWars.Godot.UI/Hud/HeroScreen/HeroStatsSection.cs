using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Magic;
using EternalWars.Core.Events;
using EternalWars.Godot.Shared.Controls;
using EternalWars.Godot.Shared.Events;
using Godot;

namespace EternalWars.Godot.UI.Hud.HeroScreen;

/// <summary>
/// The four primary stats (with what artifacts add), mana and experience, each after its icon; morale, luck, movement, scouting and waiting
/// level-ups; what the artifacts give; the specialty; the biography the map's author wrote (ADR-257).
/// </summary>
internal sealed class HeroStatsSection(HeroPanel panel) : HeroSection(panel)
{
    public HFlowContainer Badges { get; } = new();

    public Label Stats { get; } = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(UiTheme.ScreenTextWidth, 0) };

    public Label Specialty { get; } = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(UiTheme.ScreenTextWidth, 0) };

    public Label Biography { get; } = new()
    {
        AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(UiTheme.ScreenTextWidth, 0), Modulate = UiTheme.TextQuiet,
    };

    protected override Node Anchor => Stats;

    public override void Show(Hero hero)
    {
        Badges.ClearChildren();
        var bonus = hero.Bonus;
        Badges.AddChild(HeroIcons.StatBadge(HeroStatIcon.Attack, "Attack", WithBonus(hero.Attack, bonus.Attack)));
        Badges.AddChild(HeroIcons.StatBadge(HeroStatIcon.Defense, "Defense", WithBonus(hero.Defense, bonus.Defense)));
        Badges.AddChild(HeroIcons.StatBadge(HeroStatIcon.SpellPower, "Spell Power", WithBonus(hero.SpellPower, bonus.SpellPower)));
        Badges.AddChild(HeroIcons.StatBadge(HeroStatIcon.Knowledge, "Knowledge", WithBonus(hero.Knowledge, bonus.Knowledge)));
        Badges.AddChild(HeroIcons.StatBadge(HeroStatIcon.Mana, "Mana", $"{hero.Mana}/{hero.MaxMana}"));
        Badges.AddChild(HeroIcons.StatBadge(HeroStatIcon.Experience, "Experience", $"{hero.Experience}/{ExperienceTable.For(hero.Level + 1)}"));
        Stats.Text = StatsText(hero, State.Spells);
        Specialty.Text = hero.Specialty is { } specialty ? $"Specialty: {specialty.Describe(State.Spells)}" : "No specialty.";
        Biography.Text = hero.Biography ?? "";
        Biography.Visible = hero.Biography is not null;
    }

    /// <summary>The artifacts and the hero's state, and the army (its morale).</summary>
    protected override void Subscribe(RefreshScheduler refresh, IEventSubscriber events) =>
        OnHeroState(OnArtifacts(refresh, events), events)
            .On<ArmyChanged>(events);

    private static string WithBonus(int baseValue, int bonus) =>
        bonus == 0 ? $"{baseValue}" : $"{baseValue + bonus} ({baseValue} {(bonus > 0 ? "+" : "−")} {Math.Abs(bonus)})";

    private static string Signed(int value) => value > 0 ? $"+{value}" : $"{value}";

    private static string StatsText(Hero hero, SpellCatalog spells) =>
        string.Join("\n",
            $"Morale {Signed(hero.TotalMorale)}   Luck {Signed(hero.TotalLuck)}   Movement {hero.MovementPoints}/{hero.MaxMovementPoints}   Scouting {hero.ScoutingRadius}"
                + (hero.PendingLevelUps > 0 ? $"   {hero.PendingLevelUps} level-up(s) waiting" : ""),
            hero.Artifacts.IsEmpty ? "No artifacts." : $"Artifacts: {hero.Bonus.Describe(spells)}");
}
