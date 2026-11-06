using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Magic;
using EternalWars.Core.Domain.Turns;
using EternalWars.Godot.Shared.Events;
using Godot;

namespace EternalWars.Godot.UI.Hud;

/// <summary>
/// What the information spells show (Distant Vision, Detect Magic, Grave Whisper, Ledger of Ages, Eavesdrop): a read-only popup for the human
/// player whose hero cast the spell, filled from the spell's event (or, for Ledger of Ages III and Eavesdrop III, from <see cref="ShowLedger"/> /
/// <see cref="ShowTown"/>). Other players' armies come as <see cref="ShownArmy"/>, so Veil's amounts show here. Escape or "Close" shut it.
/// </summary>
public partial class AdventureIntelPanel : PanelContainer
{
    private readonly Label _title = new();
    private readonly Label _body = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(560, 0) };
    private GameSession _session = null!;

    public void Bind(GameSession session)
    {
        _session = session;
        this.CenterAsScreen();
        var box = new VBoxContainer();
        AddChild(box);
        var header = new HBoxContainer();
        box.AddChild(header);
        header.AddChild(_title);
        var close = new Button { Text = "Close" }.WithIcon(UiIcon.Close);
        close.Pressed += () => Visible = false;
        header.AddChild(close);
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(580, 420) };
        box.AddChild(scroll);
        scroll.AddChild(_body);
        Visible = false;

        this.SubscribeWhileInTree<ArmiesSighted>(session.Events, e =>
        {
            if (ForViewer(e.Owner))
            {
                Show($"Distant Vision around {Cell(e.Center)} (radius {e.Radius})",
                    e.Armies.Count == 0 ? "No creatures or heroes there." : string.Join("\n\n", e.Armies.Select(a => DescribeArmy(_session.State, a))));
            }
        });
        this.SubscribeWhileInTree<MagicDetected>(session.Events, e =>
        {
            if (ForViewer(e.Owner))
            {
                Show($"Detect Magic (radius {e.Radius})",
                    e.Objects.Count == 0
                        ? "Nothing holds anything here."
                        : string.Join("\n\n", e.Objects.Select(o => $"{o.Name} at {Cell(o.Position)}\n    {string.Join("\n    ", o.Contents)}")));
            }
        });
        this.SubscribeWhileInTree<GravesMarked>(session.Events, e =>
        {
            if (ForViewer(e.Owner))
            {
                var hero = _session.State.GetHero(e.Hero);
                var text = $"{hero.Name}'s next victory this week raises {e.Percent} % more with Necromancy.";
                if (e.Guards.Count > 0)
                {
                    text += "\n\nThe guards around:\n\n" + string.Join("\n\n", e.Guards.Select(g => DescribeArmy(_session.State, g)));
                }

                Show("Grave Whisper", text);
            }
        });
        this.SubscribeWhileInTree<LedgerRead>(session.Events, e =>
        {
            if (ForViewer(e.Owner))
            {
                ShowLedger(e.Report);
            }
        });
        this.SubscribeWhileInTree<TownOverheard>(session.Events, e =>
        {
            if (ForViewer(e.Owner))
            {
                ShowTown(e.Town);
            }
        });
        this.SubscribeWhileInTree<TurnStarted>(session.Events, _ => Visible = false);
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (Visible && @event is InputEventKey { Pressed: true, Keycode: Key.Escape })
        {
            Visible = false;
            GetViewport().SetInputAsHandled();
        }
    }

    /// <summary>Ledger of Ages: next week's event and the enemy heroes' armies.</summary>
    public void ShowLedger(LedgerReport report)
    {
        var state = _session.State;
        var next = report.NextWeek.Kind == WeekEventKind.None ? "an ordinary week" : report.NextWeek.Describe(state.Creatures);
        Show("Ledger of Ages",
            $"Next week: {next}.\n\n" + (report.Heroes.Count == 0
                ? "No enemy hero in sight."
                : string.Join("\n\n", report.Heroes.Select(h => DescribeArmy(state, h)))));
    }

    /// <summary>Eavesdrop: an enemy town's screen, read-only; or, with <paramref name="title"/>, a town as Scouting shows it (ADR-125).</summary>
    public void ShowTown(TownIntel town, string? title = null)
    {
        var state = _session.State;
        var owner = town.Owner is { } id ? state.GetPlayer(id).Name : "neutral";
        var lines = new List<string>();
        if (town.Buildings is { } buildings)
        {
            lines.Add($"Buildings: {(buildings.Count == 0 ? "none" : string.Join(", ", buildings))}");
        }

        lines.Add($"To recruit: {town.Available switch { null => "unknown (Expert Scouting shows it)", { Count: 0 } => "nothing", var available => Stacks(state, available) }}");
        lines.Add($"Garrison: {(town.Garrison.Count == 0 ? "empty" : Stacks(state, town.Garrison))}");
        if (town.GarrisonHero is { } inside)
        {
            lines.Add($"Inside: {DescribeArmy(state, inside)}");
        }

        if (town.VisitingHero is { } visitor)
        {
            lines.Add($"At the gate: {DescribeArmy(state, visitor)}");
        }

        Show($"{title ?? "Eavesdrop"}: {town.Name} ({town.TownType}, {owner})", string.Join("\n\n", lines));
    }

    /// <summary>
    /// A hero or a neutral stack as a player is shown it: exact counts or amounts with their ranges (Veil, Scouting); its artifacts unless hidden;
    /// what Scouting tells of a hero (level, stats, war machines, skills, spells).
    /// </summary>
    public static string DescribeArmy(GameState state, ShownArmy army)
    {
        var who = army.Owner is { } owner ? $"{army.Name} ({state.GetPlayer(owner).Name})" : army.Name;
        var text = $"{who} at {Cell(army.Position)}: {(army.Stacks.Count == 0 ? "no army" : Stacks(state, army.Stacks))}";
        if (army.Hero is not null)
        {
            text += army.ArtifactsHidden
                ? "\n    artifacts: hidden"
                : army.Artifacts.Count == 0 ? "" : $"\n    artifacts: {string.Join(", ", army.Artifacts.Select(a => state.Artifacts.TryGet(a, out var d) ? d.Name : a))}";
        }

        if (army.Details is { } details)
        {
            text += $"\n    level {details.Level}: Attack {details.Attack}, Defense {details.Defense}, Spell Power {details.SpellPower}, Knowledge {details.Knowledge}";
            if (details.WarMachines is { } machines)
            {
                text += $"\n    war machines: {(machines.Count == 0 ? "none" : string.Join(", ", machines))}";
            }

            if (details.Skills is { } skills)
            {
                text += $"\n    skills: {(skills.Count == 0 ? "none" : string.Join(", ", skills.OrderBy(s => s.Key, StringComparer.Ordinal).Select(s => $"{(BuiltInSkills.Contains(s.Key) ? BuiltInSkills.Get(s.Key).Name : s.Key)} ({s.Value})")))}";
            }

            if (details.Spells is { } spells)
            {
                text += $"\n    spells: {(spells.Count == 0 ? "none" : string.Join(", ", spells.Select(s => state.Spells.TryGet(s, out var spell) ? spell.Name : s)))}";
            }
        }

        return army.Veiled ? text + "\n    (veiled: sizes only)" : text;
    }

    private static string Stacks(GameState state, IEnumerable<ShownStack> stacks) =>
        string.Join(", ", stacks.Select(s => $"{(s.Count is { } count ? $"{count}" : s.Amount)} {(state.Creatures.TryGet(s.Creature, out var c) ? c.Name : s.Creature)}"));

    private static string Cell(MapPosition position) => $"{position.X},{position.Y}";

    private bool ForViewer(PlayerId owner) => owner == _session.State.CurrentPlayer.Id && _session.State.CurrentPlayer.IsHuman;

    private void Show(string title, string body)
    {
        _title.Text = title;
        _body.Text = body;
        Visible = true;
    }
}
