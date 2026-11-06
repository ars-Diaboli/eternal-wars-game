using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;

namespace EternalWars.Core.Domain.Editing;

public enum IssueSeverity
{
    Warning,
    Error,
}

/// <summary>A problem the validator found: how bad, what, where on the map and, for the map events, which event (the Events tab opens it).</summary>
public sealed record MapIssue(IssueSeverity Severity, string Message, MapPosition? Position = null, string? Event = null);

/// <summary>
/// Checks a scenario for problems that individual edits cannot prevent (e.g. painting water under a
/// hero). The editor shows these; errors should block "play this map".
/// </summary>
public static class MapValidator
{
    public static IReadOnlyList<MapIssue> Validate(GameState scenario)
    {
        var issues = new List<MapIssue>();
        var map = scenario.Map;

        foreach (var hero in scenario.Heroes)
        {
            if (!scenario.HasPlayer(hero.Owner))
            {
                issues.Add(new(IssueSeverity.Error, $"Hero {hero.Name} belongs to a missing player.", hero.Position));
            }

            if (!map.Contains(hero.Position) || !scenario.Terrains.IsPassable(map.GetTerrain(hero.Position)))
            {
                issues.Add(new(IssueSeverity.Error, $"Hero {hero.Name} stands on impassable terrain.", hero.Position));
            }
            else if (scenario.ObjectCellAt(hero.Position) is { } cell && !IsOwnTownEntrance(cell, hero.Owner))
            {
                issues.Add(new(IssueSeverity.Error, $"Hero {hero.Name} stands inside an object.", hero.Position));
            }

            if (hero.Army.IsEmpty)
            {
                issues.Add(new(IssueSeverity.Warning, $"Hero {hero.Name} has no army and cannot fight.", hero.Position));
            }

            if (!hero.HasSpellBook && hero.Spells.Count > 0)
            {
                issues.Add(new(IssueSeverity.Error, $"Hero {hero.Name} knows spells but has no spell book (ADR-120).", hero.Position));
            }

            if (hero.Definition is { } type && !scenario.HeroRoster.Contains(type))
            {
                issues.Add(new(IssueSeverity.Error, $"Hero {hero.Name} is of unknown type '{type}'.", hero.Position));
            }

            foreach (var (_, stack) in hero.Army.Stacks.Where(s => !scenario.Creatures.Contains(s.Stack.Creature)))
            {
                issues.Add(new(IssueSeverity.Error, $"Hero {hero.Name} has unknown creature '{stack.Creature}'.", hero.Position));
            }

            foreach (var artifact in hero.Artifacts.AllWorn.Select(w => w.Id).Concat(hero.Artifacts.Backpack).Where(a => !scenario.Artifacts.Contains(a)))
            {
                issues.Add(new(IssueSeverity.Error, $"Hero {hero.Name} has unknown artifact '{artifact}'.", hero.Position));
            }
        }

        foreach (var mapObject in scenario.Objects)
        {
            if (mapObject.Definition.Footprint.HasVisit && !scenario.Terrains.IsPassable(map.GetTerrain(mapObject.VisitPosition)))
            {
                issues.Add(new(IssueSeverity.Error, $"{mapObject.Definition.Name} has an impassable entrance.", mapObject.VisitPosition));
            }

            var name = mapObject.Definition.Name;
            switch (mapObject)
            {
                case Mine { Owner: { } owner } mine when !scenario.HasPlayer(owner):
                    issues.Add(new(IssueSeverity.Error, $"{name} belongs to a missing player.", mine.Position));
                    break;
                case TwoWayGate { LinkedGate: null } gate:
                    issues.Add(new(IssueSeverity.Warning, $"{name} is not linked.", gate.Position));
                    break;
                case TwoWayGate { LinkedGate: { } link } gate
                    when scenario.Objects.FirstOrDefault(o => o.Id == link) is not TwoWayGate partner || partner.LinkedGate != gate.Id:
                    issues.Add(new(IssueSeverity.Error, $"{name} link is not mutual.", gate.Position));
                    break;
                case OneWayGateEntrance { Exit: null } entrance:
                    issues.Add(new(IssueSeverity.Warning, $"{name} has no exit.", entrance.Position));
                    break;
                case OneWayGateEntrance { Exit: { } exit } entrance
                    when scenario.Objects.FirstOrDefault(o => o.Id == exit) is not OneWayGateExit:
                    issues.Add(new(IssueSeverity.Error, $"{name} points at something that is not a one-way exit.", entrance.Position));
                    break;
                case OneWayGateExit exit when !scenario.ObjectsOf<OneWayGateEntrance>().Any(e => e.Exit == exit.Id):
                    issues.Add(new(IssueSeverity.Warning, $"{name} is not used by any entrance.", exit.Position));
                    break;
                case ArtifactPickup { Artifact: { } artifact } pickup when !scenario.Artifacts.Contains(artifact):
                    issues.Add(new(IssueSeverity.Error, $"{name} holds the unknown artifact '{artifact}'.", pickup.Position));
                    break;
                case CreatureDwelling dwelling when !scenario.Creatures.Contains(dwelling.Creature):
                    issues.Add(new(IssueSeverity.Error, $"{name} uses unknown creature '{dwelling.Creature}'.", dwelling.Position));
                    break;
                case CreatureDwelling dwelling when scenario.Creatures.IsUpgrade(dwelling.Creature):
                    issues.Add(new(IssueSeverity.Error,
                        $"{name} sells the upgraded '{dwelling.Creature}'; map dwellings sell base creatures only ('{scenario.Creatures.BaseOf(dwelling.Creature)}').",
                        dwelling.Position));
                    break;
                case MonsterGuard monster when !scenario.Creatures.Contains(monster.Creature):
                    issues.Add(new(IssueSeverity.Error, $"Monster uses unknown creature '{monster.Creature}'.", monster.Position));
                    break;
                case Prison { Hero: { } prisoner } prison when !scenario.HeroRoster.Contains(prisoner):
                    issues.Add(new(IssueSeverity.Error, $"{name} holds the unknown hero '{prisoner}'.", prison.Position));
                    break;
                case Prison prison when !scenario.Terrains.IsPassable(scenario.Map.GetTerrain(prison.Position)):
                    issues.Add(new(IssueSeverity.Error, $"{name}: the freed hero would stand on impassable terrain.", prison.Position));
                    break;
                case Prison prison when scenario.Heroes.Any(h => h.Definition is { } d && d == prison.Hero):
                    issues.Add(new(IssueSeverity.Warning, $"{name} holds a hero who is already on the map.", prison.Position));
                    break;
                case Mine { Guard: { } guard } mine when !scenario.Creatures.Contains(guard.Creature):
                    issues.Add(new(IssueSeverity.Error, $"{name} guard uses unknown creature '{guard.Creature}'.", mine.Position));
                    break;
                case MapGarrison { Owner: { } garrisonOwner } garrison when !scenario.HasPlayer(garrisonOwner):
                    issues.Add(new(IssueSeverity.Error, $"{name} belongs to a missing player.", garrison.Position));
                    break;
                case MapGarrison garrison when garrison.Troops.Stacks.Any(s => !scenario.Creatures.Contains(s.Stack.Creature)):
                    issues.Add(new(IssueSeverity.Error, $"{name} holds an unknown creature.", garrison.Position));
                    break;
                case MapGarrison { Owner: null, Troops.IsEmpty: true } garrison:
                    issues.Add(new(IssueSeverity.Warning, $"{name} is neutral and empty: the first hero to arrive takes it.", garrison.Position));
                    break;
                case Town town:
                    issues.AddRange(ValidateTown(scenario, town).Select(m => new MapIssue(IssueSeverity.Error, $"{town.Name} {m}", town.Position)));
                    break;
                case PandorasBox box:
                    issues.AddRange(ValidateBox(scenario, box).Select(m => new MapIssue(IssueSeverity.Error, $"{name} {m}", box.Position)));
                    break;
                case QuestSite { Quest: null } site:
                    issues.Add(new(IssueSeverity.Warning, site.IsBarrier ? $"{name} has no quest: it bars every hero." : $"{name} has no quest: it stands empty.", site.Position));
                    break;
            }
        }

        for (var level = 0; level < map.Levels; level++)
        {
            foreach (var position in map.Positions(level))
            {
                if (map.GetRoad(position) != RoadType.None && !scenario.Terrains.IsPassable(map.GetTerrain(position)))
                {
                    issues.Add(new(IssueSeverity.Warning, "Road on impassable terrain.", position));
                }

                if (map.GetRiver(position) != RiverType.None && !scenario.Terrains.IsPassable(map.GetTerrain(position)))
                {
                    issues.Add(new(IssueSeverity.Warning, "River on impassable terrain.", position));
                }
            }
        }

        // Eyes of the Magi (ADR-070): at most four a colour, and a window needs an eye to show.
        foreach (var colour in Enum.GetValues<ObjectColour>())
        {
            var eyes = scenario.ObjectsOf<MagiEye>().Count(e => e.Colour == colour);
            if (eyes > MagiWindow.MaxEyes)
            {
                issues.Add(new(IssueSeverity.Error, $"{eyes} {colour.ToString().ToLowerInvariant()} Eyes of the Magi; a colour has at most {MagiWindow.MaxEyes}."));
            }

            foreach (var window in scenario.ObjectsOf<MagiWindow>().Where(w => w.Colour == colour && eyes == 0))
            {
                issues.Add(new(IssueSeverity.Warning, $"{window.Definition.Name} has no Eye of its colour to look through.", window.Position));
            }

            // Border posts no Keymaster's Tent opens (ADR-071).
            if (!scenario.ObjectsOf<KeymasterTent>().Any(t => t.Colour == colour))
            {
                foreach (var post in scenario.ObjectsOf<BorderPost>().Where(p => p.Colour == colour))
                {
                    issues.Add(new(IssueSeverity.Warning, $"{post.Definition.Name} has no Keymaster's Tent of its colour: nobody can pass.", post.Position));
                }
            }
        }

        foreach (var player in scenario.Players.Where(p => !scenario.HeroesOf(p.Id).Any() && !scenario.ObjectsOf<Town>().Any(t => t.Owner == p.Id)))
        {
            issues.Add(new(IssueSeverity.Warning, $"Player {player.Name} has no starting hero or town."));
        }

        // Campaign-like maps may start a player without a town (ADR-075): then the grace period should be off.
        if (scenario.Rules.Elimination && scenario.Rules.GraceDays > 0)
        {
            foreach (var player in scenario.Players.Where(p => scenario.HeroesOf(p.Id).Any() && !scenario.ObjectsOf<Town>().Any(t => t.Owner == p.Id)))
            {
                issues.Add(new(IssueSeverity.Warning,
                    $"Player {player.Name} starts without a town and loses after {scenario.Rules.GraceDays} days; for a map without towns set the grace period to 0 and add a 'lose a hero' condition."));
            }
        }

        // The editor refuses this; a map made elsewhere loses the surplus when the game starts (ADR-068).
        foreach (var player in scenario.Players.Where(p => scenario.HeroesOf(p.Id).Count() > scenario.HeroLimit(p.Id)))
        {
            issues.Add(new(IssueSeverity.Warning,
                $"Player {player.Name} starts with more heroes than it may have ({scenario.HeroLimit(player.Id)}); the newest are removed when the game starts."));
        }

        issues.AddRange(ValidateRules(scenario));
        issues.AddRange(ValidateBans(scenario));
        issues.AddRange(ValidatePlaceholders(scenario));
        issues.AddRange(ValidateEvents(scenario));
        issues.AddRange(ValidateGrail(scenario));
        return issues;
    }

    /// <summary>
    /// The Grail (ADR-140): where the author buried it a hero must be able to dig (an object or road put there later makes it unreachable); a buried
    /// Grail without obelisks can only be found by chance; it must lie <see cref="GrailRules.EdgeDistance"/> cells from the edge, so a map smaller than
    /// <see cref="GrailRules.MinMapSize"/> has none and its obelisks are pointless; a map holds at most <see cref="GrailRules.MaxObelisks"/> obelisks.
    /// </summary>
    private static IEnumerable<MapIssue> ValidateGrail(GameState scenario)
    {
        var obelisks = scenario.ObjectsOf<Obelisk>().ToList();
        if (obelisks.Count > GrailRules.MaxObelisks)
        {
            yield return new(IssueSeverity.Error, $"The map has {obelisks.Count} obelisks; at most {GrailRules.MaxObelisks} are allowed.", obelisks[GrailRules.MaxObelisks].Position);
        }

        if (obelisks.Count > 0 && !GrailRules.HoldsGrail(scenario.Map))
        {
            yield return new(IssueSeverity.Warning,
                $"The map is smaller than {GrailRules.MinMapSize}×{GrailRules.MinMapSize}, so it has no Grail and its obelisks show nothing.", obelisks[0].Position);
        }

        if (scenario.Grail.Position is not { } grail)
        {
            yield break;
        }

        if (!GrailRules.FarFromEdge(scenario.Map, grail))
        {
            yield return new(IssueSeverity.Error, $"The Grail lies nearer the map's edge than {GrailRules.EdgeDistance} cells.", grail);
        }

        if (GrailRules.CheckGround(scenario, grail) is { } problem)
        {
            yield return new(IssueSeverity.Error, $"Nobody can dig up the Grail: {problem}", grail);
        }

        if (!scenario.ObjectsOf<Obelisk>().Any())
        {
            yield return new(IssueSeverity.Warning, "The Grail is buried but the map has no obelisks: players find it only by digging at random.", grail);
        }
    }

    /// <summary>
    /// The map's bans (ADR-126): each must name something that exists; what the author placed by hand that a ban names stays in the game, so it is
    /// pointed out (a hero of a banned type or with banned artifacts, spells or skills, a prisoner, an artifact on the map, a shrine's spell, a guild's
    /// fixed spell, a witch hut's skill).
    /// </summary>
    private static IEnumerable<MapIssue> ValidateBans(GameState scenario)
    {
        var bans = scenario.Rules.Bans;
        if (bans.IsEmpty)
        {
            yield break;
        }

        foreach (var (kind, ids, known) in new (string, IReadOnlyList<string>, Func<string, bool>)[]
                 {
                     ("hero", bans.Heroes, scenario.HeroRoster.Contains), ("artifact", bans.Artifacts, scenario.Artifacts.Contains),
                     ("spell", bans.Spells, id => scenario.Spells.TryGet(id, out _)), ("skill", bans.Skills, Heroes.BuiltInSkills.Contains),
                 })
        {
            foreach (var id in ids.Where(i => !known(i)))
            {
                yield return new(IssueSeverity.Error, $"The bans name the unknown {kind} '{id}'.");
            }
        }

        const string Kept = "(placed by hand, so it stays)";
        foreach (var hero in scenario.Heroes)
        {
            if (!hero.IsPlaceholder && hero.Definition is { } type && bans.BansHero(type))
            {
                yield return new(IssueSeverity.Warning, $"Hero {hero.Name} is of a banned type {Kept}.", hero.Position);
            }

            foreach (var artifact in hero.Artifacts.AllWorn.Select(w => w.Id).Concat(hero.Artifacts.Backpack).Where(bans.BansArtifact).Distinct())
            {
                yield return new(IssueSeverity.Warning, $"Hero {hero.Name} carries the banned artifact '{artifact}' {Kept}.", hero.Position);
            }

            foreach (var spell in hero.IsPlaceholder ? [] : hero.Spells.Keys.Where(bans.BansSpell))
            {
                yield return new(IssueSeverity.Warning, $"Hero {hero.Name} knows the banned spell '{spell}' {Kept}.", hero.Position);
            }

            foreach (var skill in hero.IsPlaceholder ? [] : hero.Skills.Levels.Keys.Where(bans.BansSkill))
            {
                yield return new(IssueSeverity.Warning, $"Hero {hero.Name} has the banned skill '{skill}': it keeps it, but it is never offered.", hero.Position);
            }
        }

        foreach (var mapObject in scenario.Objects)
        {
            var name = mapObject.Definition.Name;
            var banned = mapObject switch
            {
                Prison { Hero: { } prisoner } when bans.BansHero(prisoner) => $"holds the banned hero '{prisoner}'",
                ArtifactPickup { Artifact: { } artifact } when bans.BansArtifact(artifact) => $"is the banned artifact '{artifact}'",
                Shrine { Spell: { } spell } when bans.BansSpell(spell) => $"teaches the banned spell '{spell}'",
                WitchHut hut when hut.Skills.FirstOrDefault(bans.BansSkill) is { } skill => $"teaches the banned skill '{skill}'",
                Town town when town.FixedGuildSpells.FirstOrDefault(bans.BansSpell) is { } spell => $"has the banned spell '{spell}' fixed in its guild",
                PandorasBox box when box.Contents.Artifacts.FirstOrDefault(bans.BansArtifact) is { } artifact => $"holds the banned artifact '{artifact}'",
                _ => null,
            };
            if (banned is not null)
            {
                yield return new(IssueSeverity.Warning, $"{(mapObject is Town t ? t.Name : name)} {banned} {Kept}.", mapObject.Position);
            }
        }
    }

    /// <summary>Random monsters, towns and hero placeholders (ADR-126): a monster's tier must have creatures to draw.</summary>
    private static IEnumerable<MapIssue> ValidatePlaceholders(GameState scenario)
    {
        foreach (var monster in scenario.ObjectsOf<MonsterGuard>().Where(m => m.RandomTier is not null))
        {
            if (monster.RandomTier is < 0 or > Creatures.CreatureDefinition.MaxTier)
            {
                yield return new(IssueSeverity.Error, $"A random monster has the tier {monster.RandomTier}; tiers go from 1 to {Creatures.CreatureDefinition.MaxTier} (0 = any).", monster.Position);
            }
            else if (PlaceholderRules.MonsterCandidates(scenario.Creatures, monster.RandomTier!.Value).Count == 0)
            {
                yield return new(IssueSeverity.Warning, $"No creature of tier {monster.RandomTier} can be drawn for the random monster; it stays {monster.Creature}.", monster.Position);
            }
        }
    }

    /// <summary>
    /// The map events (EVT-6 c): what stops the map loading (<see cref="MapEvents.MapEventRules"/>) as errors, then the warnings of
    /// <see cref="MapEvents.MapEventChecks"/>; each at the event's first cell, quest object or town when it has one.
    /// </summary>
    private static IEnumerable<MapIssue> ValidateEvents(GameState scenario)
    {
        var set = scenario.MapEvents.Set;
        MapPosition? Where(MapEvents.MapEventProblem problem) => problem.Position ?? (problem.Event is not { } name ? null
            : set.Cells.FirstOrDefault(c => c.Event == name)?.Position
              ?? scenario.ObjectsOf<QuestSite>().FirstOrDefault(q => q.Quest == name)?.Position
              ?? set.Find(name)?.Towns.Select(t => scenario.FindObject(new MapObjectId(t))?.Position).FirstOrDefault(p => p is not null));
        foreach (var error in MapEvents.MapEventRules.Check(scenario, set))
        {
            yield return new(IssueSeverity.Error, error.Message, Where(error), error.Event);
        }

        foreach (var warning in MapEvents.MapEventChecks.Warnings(scenario, set))
        {
            yield return new(IssueSeverity.Warning, warning.Message, Where(warning), warning.Event);
        }
    }

    /// <summary>Victory and defeat conditions (ADR-057): what they point at must exist, and a game should be able to end.</summary>
    private static IEnumerable<MapIssue> ValidateRules(GameState scenario)
    {
        var rules = scenario.Rules;
        if (rules.WeekEvents.Problem() is { } weekEvents)
        {
            yield return new(IssueSeverity.Error, weekEvents);
        }
        else if (rules.WeekEvents.Enabled)
        {
            // Without creatures to pick, a week or month of a creature quietly becomes an ordinary week.
            if (Turns.WeekEventRules.Candidates(scenario.Creatures, rules.WeekEvents.Weekly).Count == 0)
            {
                yield return new(IssueSeverity.Warning, "No faction is chosen for the weekly events: there will be no week of a creature.");
            }

            if (Turns.WeekEventRules.Candidates(scenario.Creatures, rules.WeekEvents.Monthly).Count == 0)
            {
                yield return new(IssueSeverity.Warning, "No faction is chosen for the monthly events: there will be no month of a creature.");
            }
        }

        foreach (var condition in rules.Victory.Concat(rules.Defeat))
        {
            var name = ScenarioRules.Describe(scenario, condition);
            if (condition.Player is { } player && !scenario.HasPlayer(player))
            {
                yield return new(IssueSeverity.Error, $"Condition '{condition.Kind}' belongs to a missing player.");
                continue;
            }

            var town = condition.Town is { } townId ? scenario.FindObject(townId) as Town : null;
            var hero = condition.Hero is { } heroId ? scenario.Heroes.FirstOrDefault(h => h.Id == heroId) : null;
            var target = condition.Target is { } targetId ? scenario.FindObject(targetId) : null;
            var artifactKnown = condition.Artifact is { } artifactId && scenario.Artifacts.Contains(artifactId);
            if (condition.Deadline is { } deadline)
            {
                if (!condition.CanHaveDeadline)
                {
                    yield return new(IssueSeverity.Error, $"'{name}': only a victory condition other than surviving may have a deadline.");
                }
                else if (deadline < 2)
                {
                    yield return new(IssueSeverity.Error, $"'{name}': a deadline needs a day after the first.");
                }
            }

            switch (condition.Kind)
            {
                case ConditionKind.CaptureObject when target is not IFlaggable:
                    yield return new(IssueSeverity.Error, $"Condition '{condition.Kind}' points at no object a player can own (a mine, dwelling, provider, garrison or town).");
                    break;
                case ConditionKind.CaptureObject when ((IFlaggable)target).Owner is { } owner && (condition.Player is null ? scenario.HasPlayer(owner) : owner == condition.Player):
                    yield return new(IssueSeverity.Warning, $"'{name}': the object is owned from the start, so the game would end at once.", target!.Position);
                    break;
                case ConditionKind.ReleaseHero when target is not Prison:
                    yield return new(IssueSeverity.Error, $"Condition '{condition.Kind}' points at a prison that does not exist.");
                    break;
                case ConditionKind.ObtainArtifact or ConditionKind.LoseArtifact when !artifactKnown:
                    yield return new(IssueSeverity.Error, $"Condition '{condition.Kind}' names an unknown artifact.");
                    break;
                case ConditionKind.LoseArtifact when condition.Player is null:
                    yield return new(IssueSeverity.Error, $"'{name}' needs the player who loses.");
                    break;
                case ConditionKind.LoseArtifact when !scenario.HeroesOf(condition.Player!.Value).Any(h => h.Artifacts.Has(condition.Artifact!)):
                    yield return new(IssueSeverity.Warning, $"'{name}': no hero of the player carries it at the start, so it would lose at once.");
                    break;
                case ConditionKind.ObtainArtifact when scenario.Heroes.Any(h => h.Artifacts.Has(condition.Artifact!) && (condition.Player is null ? scenario.HasPlayer(h.Owner) : h.Owner == condition.Player)):
                    yield return new(IssueSeverity.Warning, $"'{name}': a hero carries it from the start, so the game would end at once.");
                    break;
                case ConditionKind.CaptureTown or ConditionKind.LoseTown when town is null:
                    yield return new(IssueSeverity.Error, $"Condition '{condition.Kind}' points at a town that does not exist.");
                    break;
                case ConditionKind.DefeatHero or ConditionKind.LoseHero when hero is null:
                    yield return new(IssueSeverity.Error, $"Condition '{condition.Kind}' points at a hero that does not exist.");
                    break;
                case ConditionKind.LoseTown or ConditionKind.LoseHero when condition.Player is null:
                    yield return new(IssueSeverity.Error, $"'{name}' needs the player who loses.");
                    break;
                case ConditionKind.CaptureTown when town!.Owner is { } owner && (condition.Player is null ? scenario.HasPlayer(owner) : owner == condition.Player):
                    yield return new(IssueSeverity.Warning, $"'{name}': the town is owned from the start, so the game would end at once.", town.Position);
                    break;
                case ConditionKind.LoseTown when town!.Owner != condition.Player:
                    yield return new(IssueSeverity.Warning, $"'{name}': the player does not own the town at the start, so it would lose at once.", town.Position);
                    break;
                case ConditionKind.DefeatHero when condition.Player is { } winner && (winner == hero!.Owner || scenario.AreAllies(winner, hero.Owner)):
                    yield return new(IssueSeverity.Warning, $"'{name}': the hero is on the same team.", hero.Position);
                    break;
                case ConditionKind.GatherResources when condition.Amount < 1:
                    yield return new(IssueSeverity.Error, "A resource condition needs an amount above zero.");
                    break;
                case ConditionKind.SurviveUntilDay when condition.Day < 2:
                    yield return new(IssueSeverity.Error, "Surviving needs a day after the first.");
                    break;
                case ConditionKind.TimeLimit when condition.Day < 1:
                    yield return new(IssueSeverity.Error, "A time limit needs a day of 1 or more.");
                    break;
            }
        }

        if (!rules.CanBeWon)
        {
            yield return new(IssueSeverity.Warning, "Nobody can win this scenario: the standard victory is off and there is no victory condition.");
        }
        else if (rules.StandardVictory && rules.Victory.Count == 0
                 && scenario.Players.Where(p => scenario.HeroesOf(p.Id).Any() || scenario.ObjectsOf<Town>().Any(t => t.Owner == p.Id)).Select(p => p.Team).Distinct().Count() < 2)
        {
            yield return new(IssueSeverity.Warning, "Fewer than two teams start with a hero or a town, so the game cannot end by defeating enemies.");
        }

        // Team counts (ADR-117): the default teams must fit them, and with locked teams nothing at setup can fix them.
        var teams = scenario.Players.Select(p => p.Team).Distinct().Count();
        if (scenario.TeamCountProblem(teams) is { } problem)
        {
            yield return new(scenario.TeamsLocked ? IssueSeverity.Error : IssueSeverity.Warning, $"The default teams do not fit the team counts: {problem}");
        }

        if (scenario.MinTeams > scenario.Players.Count)
        {
            yield return new(IssueSeverity.Error, $"The map asks for at least {scenario.MinTeams} teams but has {scenario.Players.Count} players.");
        }
    }

    /// <summary>A hero may start in the entrance of its own town, visiting it (HoMM3; generated maps put the start hero there).</summary>
    internal static bool IsOwnTownEntrance((MapObject Object, FootprintCell Cell) cell, PlayerId owner) =>
        cell is { Object: Town town, Cell: FootprintCell.Visit } && town.Owner == owner;

    /// <summary>A Pandora's Box (ADR-100): it must hold something (or run a hero event), only gains, and only what exists.</summary>
    private static IEnumerable<string> ValidateBox(GameState scenario, PandorasBox box)
    {
        if (box.Contents.IsEmpty && box.Event is null)
        {
            yield return "is empty; give it something to hold or an event to run.";
        }

        // An event that is not a hero event is reported with the map events.
        if (box.Contents.Problem() is { } problem)
        {
            yield return problem;
        }

        foreach (var artifact in box.Contents.Artifacts.Where(a => !scenario.Artifacts.Contains(a)))
        {
            yield return $"holds the unknown artifact '{artifact}'.";
        }

        foreach (var stack in box.Contents.Creatures.Concat(box.Guard).Where(s => !scenario.Creatures.Contains(s.Creature)))
        {
            yield return $"has unknown creature '{stack.Creature}'.";
        }
    }

    private static IEnumerable<string> ValidateTown(GameState scenario, Town town)
    {
        if (town.Owner is { } owner && !scenario.HasPlayer(owner))
        {
            yield return "belongs to a missing player.";
        }

        foreach (var (building, level) in town.Buildings.Where(b => b.Value > TownRules.Buildings(scenario, town).MaxLevel(b.Key)))
        {
            yield return $"has {building} level {level}, which does not exist.";
        }

        if (TownRules.UnmetRequirement(scenario, town) is { } requirement)
        {
            yield return $"breaks a building requirement: {requirement}";
        }

        if (town.Owner is { } capitolOwner && town.Level(BuiltInBuildings.TownHall) >= BuiltInBuildings.CapitolLevel
            && TownRules.CapitolOf(scenario, capitolOwner, except: town) is { } first && first.Id.Value < town.Id.Value)
        {
            yield return $"is a second Capitol of its owner ({first.Name} is one); a player has only one.";
        }

        var lines = TownRules.Lines(scenario, town);
        foreach (var (line, level) in town.Dwellings)
        {
            if (lines.FirstOrDefault(l => l.Id == line) is not { } definition)
            {
                yield return $"has a dwelling for '{line}', which is not one of its creature lines.";
            }
            else if (level > 1 && definition.UpgradesTo is null)
            {
                yield return $"has an upgraded {definition.Name} dwelling, but that line has no upgrade.";
            }
        }

        if (town.Dwellings.Keys.FirstOrDefault() is { } any && TownRules.CheckDwellingChoice(scenario, town, any, town.DwellingLevel(any)) is { } choice)
        {
            yield return $"breaks the dwelling rule: {choice}";
        }

        foreach (var (_, stack) in town.Garrison.Stacks.Where(s => !scenario.Creatures.Contains(s.Stack.Creature)))
        {
            yield return $"garrison has unknown creature '{stack.Creature}'.";
        }

        if (Magic.MagicRules.GuildSpellsProblem(scenario.Spells, town.TownType, town.FixedGuildSpells) is { } spells)
        {
            yield return $"has wrong guild spells: {spells}";
        }
    }
}
