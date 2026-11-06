using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Godot.Shared.Events;
using Godot;

namespace EternalWars.Godot.UI.Hud;

/// <summary>
/// A short message from the map for the player whose turn it is (ADR-068): a sign's text, why an object turned the hero away, an emptied
/// object, a freed prisoner, what a Pandora's Box held, what an Astrologer's Tower foretells, what a town's unique building gave (ADR-138), a sphinx's verdict, and the week's event when a human's first turn of the week starts (ADR-073).
/// Hidden again by "OK", Escape or the hero's next step.
/// </summary>
public partial class NoticePanel : PanelContainer
{
    private readonly Label _text = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(360, 0) };
    private GameSession _session = null!;

    public void Bind(GameSession session)
    {
        _session = session;
        var box = new VBoxContainer();
        AddChild(box);
        box.AddChild(_text);
        var ok = new Button { Text = "OK" }.WithIcon(UiIcon.Confirm);
        ok.Pressed += () => Visible = false;
        box.AddChild(ok);
        Visible = false;

        this.SubscribeWhileInTree<SignRead>(session.Events, e => Show(e.Hero, $"The sign reads: \"{e.Message}\""));
        this.SubscribeWhileInTree<MapObjectRefused>(session.Events, e => Show(e.Hero, e.Reason));
        this.SubscribeWhileInTree<MapObjectFoundEmpty>(session.Events, e => Show(e.Hero, $"Nothing more to find at the {NameOf(e.Object)} for now."));
        this.SubscribeWhileInTree<HeroFreed>(session.Events, e => Show(e.Hero, $"{_session.State.GetHero(e.Hero).Name} (level {e.Level}) is free and joins you."));
        this.SubscribeWhileInTree<CreatureBankPlundered>(session.Events, e =>
            Show(e.Hero, $"The {NameOf(e.Bank)} is yours to plunder: {Core.Domain.Objects.ResourceProviderRules.Describe(e.Resources)}"
                         + (e.Artifact is { } artifact ? $" and {_session.State.Artifacts.Get(artifact).Name}." : ".")));
        this.SubscribeWhileInTree<PandorasBoxOpened>(session.Events, e => Show(e.Hero, BoxText(e)));
        this.SubscribeWhileInTree<AstrologerConsulted>(session.Events, e =>
            Show(e.Hero, e.Next.Kind == Core.Domain.Turns.WeekEventKind.None
                ? "The astrologer reads nothing unusual in the stars: next week will be an ordinary week."
                : $"The astrologer reads the stars. Next week: {e.Next.Describe(_session.State.Creatures)}"));
        this.SubscribeWhileInTree<ObeliskVisited>(session.Events, e =>
            Show(e.Hero, Core.Domain.Objects.GrailRules.Puzzle(_session.State, e.Player) is { } puzzle
                ? $"The obelisk's carvings add to your puzzle map: {puzzle.Revealed} of {puzzle.Pieces.Count} pieces (Puzzle map, P)."
                  + (puzzle.Grail is null ? "" : " The last piece is in place: a cross marks where the Grail lies.")
                : "The obelisk's carvings tell of a Grail, but no Grail lies on this map."));
        this.SubscribeWhileInTree<HoleDug>(session.Events, e =>
            Show(e.Hero, e.Grail is { } grail ? $"{_session.State.GetHero(e.Hero).Name} digs up the {_session.State.Artifacts.Get(grail).Name}! Bring it into one of your towns."
                : _session.State.Grail.Position == e.Position && !_session.State.Grail.IsFound ? "Something lies here, but there is no room left to carry it."
                : "Nothing here but earth and stones."));
        this.SubscribeWhileInTree<GrailBuildingRaised>(session.Events, e =>
            Show(e.Hero, $"The Grail is laid to rest, and {e.Name} rises in {(_session.State.FindObject(e.Town) is Core.Domain.Towns.Town raisedIn ? raisedIn.Name : "your town")}: +5000 gold a day, +50 % growth, and stronger defenders in a siege."));
        this.SubscribeWhileInTree<UniqueBuildingVisited>(session.Events, e => Show(e.Hero, UniqueText(e)));
        this.SubscribeWhileInTree<SphinxAnswered>(session.Events, e => Show(e.Hero,
            !e.Right ? $"Wrong. The sphinx's stare ends {_session.State.GetHero(e.Hero).Name}'s day ({Core.Domain.Objects.Sphinx.WrongMorale} morale for the next battle)."
            : e.Artifact is { } prize ? $"Right. The sphinx gives {_session.State.GetHero(e.Hero).Name} the {_session.State.Artifacts.Get(prize).Name}."
            : e.Experience > 0 ? $"Right. The sphinx's lesson gives {_session.State.GetHero(e.Hero).Name} {e.Experience} experience."
            : "Right, but there is no room left to carry the sphinx's prize."));
        this.SubscribeWhileInTree<TurnStarted>(session.Events, _ => AnnounceWeek());
        this.SubscribeWhileInTree<HeroMoved>(session.Events, _ => Visible = false);
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (Visible && @event is InputEventKey { Pressed: true, Keycode: Key.Escape })
        {
            Visible = false;
            GetViewport().SetInputAsHandled();
        }
    }

    /// <summary>Each human sees the week's event on its first turn of the week (hot-seat players each get it).</summary>
    private void AnnounceWeek()
    {
        var state = _session.State;
        if (state.Date.IsFirstDayOfWeek && state.CurrentPlayer.IsHuman && state.WeekEvent.Kind != Core.Domain.Turns.WeekEventKind.None)
        {
            _text.Text = $"{(state.Date.Week == 1 ? "A new month" : "A new week")} begins. {state.WeekEvent.Describe(state.Creatures)}";
            Visible = true;
        }
    }

    /// <summary>The box's message, then what the hero got (ADR-100).</summary>
    private string BoxText(PandorasBoxOpened opened)
    {
        var state = _session.State;
        var c = opened.Received;
        var gains = new List<string>();
        if (!c.Resources.IsEmpty)
        {
            gains.Add(Core.Domain.Objects.ResourceProviderRules.Describe(c.Resources));
        }

        foreach (var (amount, what) in new[] { (c.Experience, "experience"), (c.Mana, "mana"), (c.Attack, "Attack"), (c.Defense, "Defense"), (c.SpellPower, "Spell Power"), (c.Knowledge, "Knowledge") })
        {
            if (amount > 0)
            {
                gains.Add($"{amount} {what}");
            }
        }

        if (c.Morale > 0)
        {
            gains.Add($"+{c.Morale} morale for the next battle");
        }

        if (c.Luck > 0)
        {
            gains.Add($"+{c.Luck} luck for the next battle");
        }

        gains.AddRange(c.Skills.Select(s => $"{Core.Domain.Heroes.BuiltInSkills.Get(s.Key).Name} ({s.Value})"));
        gains.AddRange(c.Artifacts.Select(a => state.Artifacts.TryGet(a, out var artifact) ? artifact.Name : a));
        gains.AddRange(c.Creatures.Select(s => $"{s.Count} × {(state.Creatures.TryGet(s.Creature, out var creature) ? creature.Name : s.Creature)}"));
        var found = gains.Count == 0 ? "The box is empty, or nothing in it was of use." : $"Inside: {string.Join(", ", gains)}.";
        return opened.Message is { } message ? $"{message}\n\n{found}" : found;
    }

    /// <summary>What a town's unique building gave the visiting hero (ADR-138).</summary>
    private string UniqueText(UniqueBuildingVisited visited)
    {
        var state = _session.State;
        var hero = state.GetHero(visited.Hero);
        var name = state.FindObject(visited.Town) is Core.Domain.Towns.Town town
            ? Core.Domain.Towns.TownRules.Buildings(state, town).Get(visited.Building, 1).Name
            : visited.Building;
        var gift = Core.Domain.Towns.UniqueBuildings.Get(visited.Building)?.Bonus switch
        {
            Core.Domain.Towns.UniqueBonus.Stables => $"+{Core.Domain.Objects.BuffRules.StablesMovement} movement today and every day until the week ends",
            Core.Domain.Towns.UniqueBonus.Attack => "+1 Attack",
            Core.Domain.Towns.UniqueBonus.Defense => "+1 Defense",
            Core.Domain.Towns.UniqueBonus.Knowledge => "+1 Knowledge",
            Core.Domain.Towns.UniqueBonus.SpellPower => "+1 Spell Power",
            Core.Domain.Towns.UniqueBonus.Experience => $"+{Core.Domain.Towns.UniqueBuildings.ExperienceBonus} experience",
            Core.Domain.Towns.UniqueBonus.ManaSpring => $"mana {hero.Mana}/{hero.MaxMana}",
            _ => "its blessing",
        };
        return $"{hero.Name} visits the {name}: {gift}.";
    }

    private string NameOf(Core.Domain.Common.MapObjectId id) =>
        _session.State.FindObject(id)?.Definition.Name.ToLowerInvariant() ?? "place";

    /// <summary>Only the player whose hero it is, and only a human, sees it.</summary>
    private void Show(Core.Domain.Common.HeroId hero, string text)
    {
        var state = _session.State;
        if (state.Heroes.FirstOrDefault(h => h.Id == hero) is not { } found || found.Owner != state.CurrentPlayer.Id || !state.CurrentPlayer.IsHuman)
        {
            return;
        }

        _text.Text = text;
        Visible = true;
    }
}
