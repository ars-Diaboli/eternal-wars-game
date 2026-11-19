using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Artifacts;
using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Magic;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Pathfinding;
using EternalWars.Core.Domain.Towns;

namespace EternalWars.Core.Domain.AI;

/// <summary>
/// The value of the places a hero could go to (ADR-059): everything its player has explored that is worth a visit, in gold, with who has to
/// be beaten there; the choice among them, weighed by the route's cost and a simulated fight; and the exploring tile when nothing beats
/// going into the unknown. It keeps one route cost map per hero and game moment, so each candidate's cost is a lookup; only the target
/// chosen is searched in full, by the move. The costs are those of <see cref="HeroMovementService.FindPath"/>.
/// </summary>
internal sealed partial class TargetValuer(AiContext ai, ThreatMap threats)
{
    private readonly AiContext _ai = ai;
    private readonly ThreatMap _threats = threats;
    private readonly GameSession _session = ai.Session;
    private readonly GameState _state = ai.State;
    private readonly AiWeights _weights = ai.Weights;

    private PathFinder? _paths;
    private PathFinder.CostMap? _costs;
    private (HeroId Hero, long Stamp) _costsFor;
    private PathFinder.ReverseCostMap? _costsTo;

    private PathFinder Paths => _paths ??= new PathFinder(_state);

    /// <summary>The cost of the hero's cheapest route to <paramref name="target"/>, or null when there is none.</summary>
    public int? WalkCost(Hero hero, MapPosition target)
    {
        // The game changes only through commands (and moves): until one runs, the same map answers.
        var key = (hero.Id, _session.ChangeStamp);
        if (_costs is null || _costsFor != key)
        {
            _costs = Paths.CostsFrom(hero.Id, reuse: _costs);
            _costsFor = key;
        }

        return _costs.CostTo(target);
    }

    /// <summary>
    /// The costs of the hero's cheapest routes to <paramref name="target"/> from tiles it does not stand on (Dimension Door landings); good
    /// until the game changes.
    /// </summary>
    public PathFinder.ReverseCostMap CostsTo(Hero hero, MapPosition target) => _costsTo = Paths.CostsTo(hero.Id, target, reuse: _costsTo);

    /// <summary>
    /// What finding it is worth to the hero (ADR-121): a piece of an assembled relic counts half again for each other piece the hero already has
    /// (worn or carried), so the AI goes for the last pieces.
    /// </summary>
    public double FindScore(Hero hero, ArtifactDefinition artifact) =>
        AdventureAi.Score(artifact) * (1 + (0.5 * (_state.Artifacts.RelicOf(artifact.Id) is { } relic
            ? _state.Artifacts.Get(relic).Pieces.Count(p => p != artifact.Id && hero.Artifacts.Has(p))
            : 0)));

    private long GuardPower(MapPosition position, bool pickup = false) =>
        _ai.GuardsToBeat(position, pickup).Sum(m => _ai.Power(_ai.SeenMonster(m)));

    /// <summary>
    /// What a visit to a resource provider is worth: what waits at an own one; for another's, a week's output on average (what flagging a
    /// neutral one brings at once), doubled for the weeks to come (ADR-079).
    /// </summary>
    private double ProviderValue(Hero hero, ResourceProvider provider) =>
        provider.Owner == hero.Owner ? AiContext.Value(provider.Stored) : 2 * Math.Max(AiContext.Value(provider.Stored), WeeklyValue(provider));

    /// <summary>A provider's average week, without rolling.</summary>
    private double WeeklyValue(ResourceProvider provider) => provider.Rule switch
    {
        ProviderRule.WaterWheel => _state.Date.AbsoluteWeek == 1 ? ResourceProviderRules.WaterWheelFirstWeek : ResourceProviderRules.WaterWheelGold,
        ProviderRule.Warehouse when provider.Definition.Resource == ResourceType.Gold => provider.Definition.Amount,
        ProviderRule.Warehouse => (AiContext.GoldValue(provider.Definition.Resource!.Value, provider.Definition.Amount) + ResourceProviderRules.WarehouseGold) / 2,
        ProviderRule.Windmill => 4.5 * ((2 * AiContext.GoldValue(ResourceType.Ore, 1)) + (3 * AiContext.GoldValue(ResourceType.Crystal, 1))) / 5,
        _ => (ResourceProviderRules.GardenGold + AiContext.GoldValue(ResourceType.Crystal, ResourceProviderRules.GardenMagic)) / 2,
    };

    /// <summary>An artifact to pick up, in gold: <see cref="AiWeights.ArtifactValue"/> and its score for the hero.</summary>
    private double ArtifactWorth(Hero hero, ArtifactDefinition artifact) => _weights.ArtifactValue + (_weights.ArtifactScoreValue * FindScore(hero, artifact));

    /// <summary>What training is worth to the hero now (0: not worth the trip, or the player cannot pay with gold to spare).</summary>
    private double TrainingValue(Hero hero, TrainingRule rule)
    {
        var gold = _state.GetPlayer(hero.Owner).Resources.Gold;
        return rule switch
        {
            TrainingRule.LearningStone => 1200,
            TrainingRule.Arena or TrainingRule.ColosseumOfTheMagi => 1500,
            TrainingRule.LibraryOfEnlightenment => hero.Level >= TrainingRules.LibraryLevel ? 3000 : 0,
            TrainingRule.Gazebo => gold >= TrainingRules.GazeboGold + _weights.TrainingCushion ? 1000 : 0,
            TrainingRule.DreamTeacher => 600,
            TrainingRule.TrainingGrounds or TrainingRule.MarlettoTower or TrainingRule.StarAxis or TrainingRule.GardenOfRevelation => 750,
            _ => gold >= TrainingRules.TreeGold + _weights.TrainingCushion ? 1500 : 0,
        };
    }

    /// <summary>
    /// What going to an own town is worth to the hero (0: nothing to do there): defending it when an enemy can reach it, the garrison alone cannot
    /// hold it, nobody of ours is in it and the hero can; else picking up the troops waiting there when they are worth the trip.
    /// </summary>
    public double TownNeed(Hero hero, Town own)
    {
        var power = _ai.ArmyPower(hero.Army);
        var danger = _threats.Danger(hero.Owner, own.VisitPosition);
        if (danger > 0 && !_threats.Holds(_ai.ArmyPower(own.Garrison), danger) && TownRules.VisitingHero(_state, own) is null
            && _threats.Holds(power + _ai.ArmyPower(own.Garrison), danger))
        {
            return _weights.DefendTownValue + (own.Dwellings.Count * _weights.TownDwellingValue);
        }

        // Creatures in the dwellings count only as far as the gold buys them, or they would call the hero home every day (ADR-114).
        var resources = _state.GetPlayer(hero.Owner).Resources;
        var waiting = _ai.ArmyPower(own.Garrison) + own.Available.Sum(a => _state.Creatures.Get(a.Key) is var line
            ? (long)TownRules.MaxAffordable(resources, TownRules.CreatureCost(_state, own, line), a.Value) * line.FightValue
            : 0);
        return waiting * 100 > power * _weights.TroopsTripPercent ? _weights.TroopsTripShare * Math.Min(waiting, Math.Max(power, _weights.TroopsTripFloor)) : 0;
    }

    /// <summary>The own town a town gate should take the hero to: the one it needs most whose entrance is free; null when none is worth it.</summary>
    public (Town Town, double Need)? GateTown(Hero hero) =>
        _state.ObjectsOf<Town>()
            .Where(t => t.Owner == hero.Owner && t.VisitPosition != hero.Position && _state.HeroAt(t.VisitPosition) is null)
            .Select(t => (Town: t, Need: TownNeed(hero, t)))
            .Where(t => t.Need > 0)
            .OrderByDescending(t => t.Need).ThenBy(t => t.Town.Id.Value)
            .Select(t => ((Town, double)?)t)
            .FirstOrDefault();

    /// <summary>The stacks a hill fort would upgrade for the hero, strongest first, while the spare resources last (ADR-068).</summary>
    public List<(int Slot, CreatureStack Stack, ResourceSet Cost, long Gain)> HillFortUpgrades(Hero hero)
    {
        var spare = _ai.Spare(hero.Owner);
        var upgrades = new List<(int, CreatureStack, ResourceSet, long)>();
        foreach (var (slot, stack) in hero.Army.Stacks.OrderByDescending(s => _ai.Power(s.Stack)).ThenBy(s => s.Slot))
        {
            if (HillFortRules.UpgradeOf(_state.Creatures, stack.Creature) is not { } upgraded)
            {
                continue;
            }

            var cost = HillFortRules.Cost(_state.Creatures, stack);
            if (spare.CanAfford(cost))
            {
                spare -= cost;
                // Worth what the upgrade costs in a town: the army power sum barely sees what an upgrade adds.
                upgrades.Add((slot, stack, cost, (long)AiContext.Value((upgraded.Cost - _state.Creatures.Get(stack.Creature).Cost) * stack.Count)));
            }
        }

        return upgrades;
    }

    /// <summary>What a visit to a hill fort is worth now: the town price of the upgrades the hero would buy.</summary>
    private long HillFortGain(Hero hero) => HillFortUpgrades(hero).Sum(u => u.Gain);

    /// <summary>The war machines the hero lacks and would buy at a factory, as in a town (ADR-056), with the gold cushion kept.</summary>
    public List<WarMachineSlot> FactoryWants(Hero hero)
    {
        var wanted = new List<WarMachineSlot>();
        if (WarMachines.FieldsOffensive(hero.SkillLevelOf(BuiltInSkills.Ballistics)))
        {
            wanted.Add(WarMachineSlot.Offensive);
        }

        wanted.Add(WarMachineSlot.Tent);
        if (hero.Army.Stacks.Any(s => _state.Creatures.Get(s.Stack.Creature).Shots > 0))
        {
            wanted.Add(WarMachineSlot.Cart);
        }

        var gold = _state.GetPlayer(hero.Owner).Resources.Gold;
        var bought = new List<WarMachineSlot>();
        foreach (var slot in wanted.Where(s => !hero.WarMachines.Contains(s)))
        {
            if (gold - WarMachines.Cost(slot).Gold >= _weights.WarMachineCushion)
            {
                gold -= WarMachines.Cost(slot).Gold;
                bought.Add(slot);
            }
        }

        return bought;
    }

    /// <summary>
    /// The lessons the hero would take at a University or War Academy (ADR-139): its open lessons while the spare resources last, skills it already
    /// learns first, a new group only while it has a free group slot.
    /// </summary>
    public List<Lesson> LessonsToTake(Hero hero, SkillSchool school)
    {
        var spare = _ai.Spare(hero.Owner);
        var groups = hero.Skills.Groups.Count();
        var taken = new List<Lesson>();
        foreach (var lesson in SkillSchoolRules.LessonsOf(school, hero.Id).Where(l => SkillSchoolRules.StateOf(hero, l) == LessonState.Open).OrderBy(l => l.NewGroup))
        {
            if (!spare.CanAfford(SkillSchoolRules.Price) || (lesson.NewGroup && groups >= HeroSkills.MaxGroups))
            {
                continue;
            }

            groups += lesson.NewGroup ? 1 : 0;
            spare -= SkillSchoolRules.Price;
            taken.Add(lesson);
        }

        return taken;
    }

    /// <summary>
    /// What a visit to a University or War Academy is worth: the lessons it would take; before the first visit (the lessons are drawn then) a
    /// guess of one lesson when the player can pay for one.
    /// </summary>
    private double SchoolValue(Hero hero, SkillSchool school) =>
        school.Lessons.ContainsKey(hero.Id) ? _weights.LessonValue * LessonsToTake(hero, school).Count
        : _ai.Spare(hero.Owner).CanAfford(SkillSchoolRules.Price) ? _weights.LessonValue : 0;

    /// <summary>Where the Grail lies when the player's puzzle is whole and nobody has dug it up yet (ADR-140), else null.</summary>
    public MapPosition? GrailSpot(PlayerId player) =>
        _state.Grail.IsFound ? null : GrailRules.Puzzle(_state, player)?.Grail;

    /// <summary>
    /// What a Pandora's Box is worth to the hero, by the AI's values (ADR-100): resources at their gold value, experience 1:1, a primary stat as a
    /// training site's (750), morale and luck a buff's (150 a point), a skill 1000 a level gained, artifacts as pickups (only while the backpack has
    /// room) and creatures at <see cref="AiWeights.BoxCreatureShare"/> of their value where they fit. Mana counts for nothing until there are spells.
    /// </summary>
    public double BoxValue(Hero hero, BoxContents contents)
    {
        var stats = Enum.GetValues<PrimaryStat>().Sum(contents.Stat);
        var skills = contents.Skills.Where(s => hero.Skills.CanRaise(s.Key)).Sum(s => Math.Max(0, s.Value - hero.Skills.Level(s.Key)));
        var artifacts = hero.Artifacts.BackpackFull ? 0 : contents.Artifacts.Sum(a => _state.Artifacts.TryGet(a, out var found) ? ArtifactWorth(hero, found) : 0);
        var creatures = contents.Creatures.Where(s => hero.Army.CanAccept(s.Creature)).Sum(s => _weights.BoxCreatureShare * s.Count * _state.Creatures.Get(s.Creature).FightValue);
        return AiContext.Value(contents.Resources) + contents.Experience + (750 * stats) + (150 * (contents.Morale + contents.Luck)) + (1000 * skills) + artifacts + creatures;
    }

    /// <summary>A buff is worth a short detour at most; the Magic Well nothing until there are spells.</summary>
    private static double BuffValue(BuffRule rule) => rule switch
    {
        BuffRule.Stables => 400,
        BuffRule.FountainOfYouth or BuffRule.MineralSpring or BuffRule.RallyFlag => 300,
        BuffRule.FaerieRing or BuffRule.Temple or BuffRule.CloverField or BuffRule.Rainbow => 150,
        _ => 0,
    };

    /// <summary>A loot site's average worth by the AI's values (a Minor artifact counted as 1000, the tomb's morale as a cost).</summary>
    private static double LootValue(LootRule rule)
    {
        var resource = ResourceProviderRules.NonGold.Average(r => AiContext.GoldValue(r, 1));
        return rule switch
        {
            LootRule.Campfire => 500 + (5 * resource),
            LootRule.SupplyCrates => 10 * resource,
            LootRule.TravelersWagon => (0.3 * 1000) + (0.6 * 3.5 * resource),
            LootRule.Corpse => 0.2 * 1000,
            LootRule.LeanTo => 3.5 * resource,
            LootRule.MagesChest => (0.9 * 1000) + (0.05 * 2500) + (0.3 * 750),
            _ => 1200 - 400,
        };
    }

    /// <summary>The target tile of the best move, or null when there is nothing worth doing.</summary>
    public MapPosition? ChooseTarget(Hero hero, HashSet<MapPosition> failed)
    {
        // Stay out of reach of enemy heroes too strong for the hero (with the garrison, at an own town).
        var candidates = Targets(hero)
            .Where(t => !(_ai.Preparing && t.Later) && !failed.Contains(t.Position) && !_ai.Claimed.Contains(t.Position) && t.Position != hero.Position)
            .Where(t => _threats.Holds(_threats.DefenceAt(hero, t.Position), _threats.Danger(hero.Owner, t.Position, except: t.Position)))
            .OrderByDescending(t => t.Value / (1 + (_weights.DistancePenalty * Distance(hero.Position, t.Position))))
            .ThenBy(t => t.Position.Level).ThenBy(t => t.Position.Y).ThenBy(t => t.Position.X);

        MapPosition? best = null;
        var bestScore = 0.0;
        var checkedCount = 0;
        var fights = 0;
        foreach (var candidate in candidates)
        {
            if (checkedCount >= _weights.CandidatesChecked || fights >= _weights.FightsChecked)
            {
                break;
            }

            // Fights are played out before going: the army power sum does not see shooters, speed and defence.
            if (candidate.Opponent is { } opponent)
            {
                fights++;
                if (!_ai.Estimator.Fight(hero, opponent, candidate.OpponentAttack, candidate.OpponentDefense, candidate.Siege).IsSafe(_ai.Preparing ? Math.Min(candidate.MaxLoss, _weights.ArenaLoss) : candidate.MaxLoss))
                {
                    continue;
                }
            }

            checkedCount++;
            if (WalkCost(hero, candidate.Position) is not { } cost)
            {
                continue;
            }

            var score = candidate.Value / (0.5 + Days(hero, cost));
            if (score > bestScore)
            {
                (best, bestScore) = (candidate.Position, score);
            }
        }

        // Unexplored ground is worth a trip of its own (it hides what the targets are made of), not only when nothing else is left.
        if (Explore(hero, failed) is { } explore && ExploreWorth(hero) / (0.5 + Days(hero, explore.Cost)) > bestScore)
        {
            return explore.Position;
        }

        return best;
    }

    /// <summary>A guard or garrison next to unexplored ground is also worth the exploring it opens (passage guards stand between zones).</summary>
    public double OpensTheWay(PlayerId owner, MapPosition position)
    {
        for (var dy = -_weights.WayRadius; dy <= _weights.WayRadius; dy++)
        {
            for (var dx = -_weights.WayRadius; dx <= _weights.WayRadius; dx++)
            {
                var near = new MapPosition(position.X + dx, position.Y + dy, position.Level);
                if (_state.Map.Contains(near) && !_state.Fog.IsExplored(owner, near))
                {
                    return _weights.ExploreValue;
                }
            }
        }

        return 0;
    }

    private static double Days(Hero hero, int cost) => cost / (double)Math.Max(1, hero.MaxMovementPoints);

    /// <summary>What exploring is worth to the hero now: <see cref="AiWeights.ExploreValue"/>, more for the main hero while no enemy town is known.</summary>
    private double ExploreWorth(Hero hero) =>
        _weights.ExploreValue + (hero.Id == _ai.Main && !_ai.Preparing && !_state.ObjectsOf<Town>().Any(t => t.Owner is { } o && _state.AreEnemies(hero.Owner, o) && _state.Fog.IsExplored(hero.Owner, t.VisitPosition))
            ? _weights.HuntShare * _ai.ArmyPower(hero.Army) : 0);

    /// <summary>The hero's best candidate targets and the exploring tile as text (<see cref="AdventureAi.Explain"/>).</summary>
    public IReadOnlyList<string> Explain(HeroId id, int count)
    {
        var hero = _state.GetHero(id);
        var lines = new List<string> { $"{hero.Name} at {hero.Position}, power {_ai.ArmyPower(hero.Army)}, movement {hero.MovementPoints}/{hero.MaxMovementPoints}, main {hero.Id == _ai.Main}, defending {_threats.Defending(hero)}" };
        foreach (var t in Targets(hero)
                     .OrderByDescending(t => t.Value / (1 + (_weights.DistancePenalty * Distance(hero.Position, t.Position))))
                     .Take(count))
        {
            var what = _state.ObjectCellAt(t.Position)?.Object.Definition.Id ?? (_state.HeroAt(t.Position) is { } h ? $"hero {h.Name}" : "guarded tile");
            var danger = _threats.Danger(hero.Owner, t.Position, except: t.Position);
            var safe = t.Opponent is not { } opponent ? "no fight"
                : _ai.Estimator.Fight(hero, opponent, t.OpponentAttack, t.OpponentDefense, t.Siege).IsSafe(t.MaxLoss) ? $"safe vs {_ai.ArmyPower(opponent)}" : $"UNSAFE vs {_ai.ArmyPower(opponent)}";
            var path = WalkCost(hero, t.Position) is { } cost ? $"path {cost}" : "NO PATH";
            lines.Add($"  {t.Position} {what}: value {t.Value:0}, {safe}, {path}{(_threats.Holds(_threats.DefenceAt(hero, t.Position), danger) ? string.Empty : $", danger {danger}")}");
        }

        lines.Add($"  explore: {(Explore(hero, []) is { } e ? $"{e.Position} cost {e.Cost}" : "nothing")}");
        return lines;
    }

    private static int Distance(MapPosition a, MapPosition b) =>
        a.Level != b.Level ? 40 + Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y)) : Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));

    /// <summary>A place worth going to. <see cref="Opponent"/> is who has to be beaten there, if anyone; <see cref="Siege"/> the walls it holds (ADR-106).</summary>
    private readonly record struct Target(
        MapPosition Position, double Value, Army? Opponent = null, int OpponentAttack = 0, int OpponentDefense = 0, double MaxLoss = 0.5,
        SiegeSpec? Siege = null, bool Later = false);

    /// <summary>The neutral guards of a tile as one army, or null.</summary>
    private Army? GuardArmy(MapPosition position, bool pickup = false)
    {
        var stacks = _ai.GuardsToBeat(position, pickup).Take(Army.SlotCount).Select(_ai.SeenMonster).ToArray();
        return stacks.Length == 0 ? null : Army.Of(stacks);
    }

    /// <summary>Everything the hero knows about that is worth going to, with a value in gold. Fights are checked later.</summary>
    private IEnumerable<Target> Targets(Hero hero)
    {
        var owner = hero.Owner;
        var power = _ai.ArmyPower(hero.Army);

        // Loose pre-filter on the power sum; the real check is the simulated fight.
        bool Plausible(long enemy) => enemy == 0 || (!hero.Army.IsEmpty && power * 100 >= enemy * _weights.PlausiblePercent);

        foreach (var mapObject in _state.Objects.OrderBy(o => o.Id.Value))
        {
            var visit = mapObject.VisitPosition;
            if (!_state.Fog.IsExplored(owner, visit))
            {
                continue;
            }

            switch (mapObject)
            {
                case ResourcePile pile when Plausible(GuardPower(visit, pickup: true)):
                    yield return new(visit, AiContext.GoldValue(pile.Resource, pile.Amount ?? ResourcePile.AmountRange(pile.Resource).Min), GuardArmy(visit, pickup: true), MaxLoss: _weights.MonsterLoss);
                    break;
                case ArtifactPickup { Artifact: { } artifact } when !hero.Artifacts.BackpackFull && _state.Artifacts.TryGet(artifact, out var found) && Plausible(GuardPower(visit, pickup: true)):
                    yield return new(visit, ArtifactWorth(hero, found), GuardArmy(visit, pickup: true), MaxLoss: _weights.MonsterLoss);
                    break;
                case ResourceProvider provider when (provider.Owner != hero.Owner || !provider.Stored.IsEmpty) && Plausible(GuardPower(visit)) && visit != hero.Position:
                    yield return new(visit, ProviderValue(hero, provider), GuardArmy(visit), MaxLoss: _weights.MonsterLoss, Later: provider.Stored.IsEmpty);
                    break;
                case CreatureBank { IsGuarded: true } bank when visit != hero.Position && !hero.Army.IsEmpty:
                    // Worth its reward; the simulated fight later decides whether the hero can take it (the guard stays whole if it fails).
                    var bankGuard = _ai.Seen(bank.Guard, visit, null);
                    if (Plausible(_ai.ArmyPower(bankGuard)))
                    {
                        yield return new(visit, AiContext.Value(bank.Reward) + (bank.RewardArtifact is null ? 0 : _weights.BankArtifactValue) + (_weights.BankArtifactValue * bank.MoreArtifacts.Count), bankGuard, MaxLoss: _weights.MonsterLoss);
                    }

                    break;
                case PandorasBox box when visit != hero.Position && (!box.IsGuarded || !hero.Army.IsEmpty) && BoxValue(hero, box.Contents) is > 0 and var boxWorth:
                    // Weighed by its contents against its own guard, else the monsters next to it; the simulated fight later decides.
                    var boxGuard = box.IsGuarded ? _ai.Seen(box.Guard, visit, null) : GuardArmy(visit);
                    if (Plausible(boxGuard is null ? 0 : _ai.ArmyPower(boxGuard)))
                    {
                        yield return new(visit, boxWorth, boxGuard, MaxLoss: _weights.MonsterLoss);
                    }

                    break;
                case QuestSite site when visit != hero.Position && QuestWorth(hero, site) is > 0 and var questWorth && Plausible(GuardPower(visit)):
                    // A Seer's Hut or Quest Guard whose stage the hero meets and which gives more than it takes (EVT-6); the others are obstacles.
                    yield return new(visit, questWorth, GuardArmy(visit), MaxLoss: _weights.MonsterLoss);
                    break;
                case Prison { Hero: not null } when !_state.HeroLimitReached(owner) && Plausible(GuardPower(visit)) && visit != hero.Position:
                    yield return new(visit, 2500, GuardArmy(visit), MaxLoss: _weights.MonsterLoss, Later: true); // a hero without an army
                    break;
                case Observatory observatory when !observatory.Visits.Heroes.Any(h => _state.Heroes.Any(x => x.Id == h && x.Owner == owner))
                                                  && Plausible(GuardPower(visit)) && visit != hero.Position:
                    yield return new(visit, 600, GuardArmy(visit), MaxLoss: _weights.MonsterLoss, Later: true);
                    break;
                case TrainingSite training when training.CanCollect(hero.Id, _state.Date) && TrainingValue(hero, training.Rule) is > 0 and var trainingWorth
                                                 && Plausible(GuardPower(visit)) && visit != hero.Position:
                    yield return new(visit, trainingWorth, GuardArmy(visit), MaxLoss: _weights.MonsterLoss);
                    break;
                case BuffSite buff when buff.CanCollect(hero.Id, _state.Date) && BuffValue(buff.Rule) is > 0 and var buffWorth
                                        && Plausible(GuardPower(visit)) && visit != hero.Position:
                    yield return new(visit, buffWorth, GuardArmy(visit), MaxLoss: _weights.MonsterLoss);
                    break;
                case LootSite site when site.CanCollect(hero.Id, _state.Date) && Plausible(GuardPower(visit, pickup: site.Vanishes)) && visit != hero.Position:
                    yield return new(visit, LootValue(site.Rule), GuardArmy(visit, pickup: site.Vanishes), MaxLoss: _weights.MonsterLoss);
                    break;
                case TreasureChest when Plausible(GuardPower(visit, pickup: true)):
                    yield return new(visit, _weights.ChestValue, GuardArmy(visit, pickup: true), MaxLoss: _weights.MonsterLoss);
                    break;
                case Deposit deposit when _ai.Spare(owner).CanAfford(deposit.Price) && Plausible(GuardPower(visit)) && visit != hero.Position:
                    var mineYield = _state.ObjectCatalog.Get(deposit.Definition.Becomes!);
                    yield return new(visit, (8 * mineYield.DailyYield * AiContext.GoldValue(mineYield.Resource!.Value, 1)) - (0.5 * AiContext.Value(deposit.Price)), GuardArmy(visit), MaxLoss: _weights.MonsterLoss, Later: true);
                    break;
                case Shrine { Spell: { } taught } shrine when !hero.Spells.ContainsKey(taught) && _state.Spells.TryGet(taught, out var shrineSpell)
                                                          && Magic.MagicRules.CanLearn(hero, shrineSpell) && Plausible(GuardPower(visit)) && visit != hero.Position:
                    // A spell it can learn (ADR-120): worth more the higher its level.
                    yield return new(visit, 400 * shrine.SpellLevel, GuardArmy(visit), MaxLoss: _weights.MonsterLoss);
                    break;
                case KeymasterTent tent when !_state.GetPlayer(owner).Keys.Contains(tent.Colour) && Plausible(GuardPower(visit)) && visit != hero.Position:
                    // The key opens the border posts of its colour (ADR-071): worth most once one is known to bar the way.
                    var barred = _state.ObjectsOf<BorderPost>().Any(b => b.Colour == tent.Colour && _state.Fog.IsExplored(owner, b.VisitPosition));
                    yield return new(visit, barred ? 2500 : 600, GuardArmy(visit), MaxLoss: _weights.MonsterLoss, Later: !barred);
                    break;
                case MapGarrison mapGarrison when !_ai.OwnTeam(owner, mapGarrison.Owner) && visit != hero.Position
                                                  && _ai.Seen(mapGarrison.Troops.Stacks.Select(s => s.Stack), visit, mapGarrison.Owner) is var troops && Plausible(_ai.ArmyPower(troops)):
                    yield return new(visit, 1000 + (_weights.GuardArmyShare * _ai.ArmyPower(troops)) + OpensTheWay(owner, visit), troops.IsEmpty ? null : troops, MaxLoss: _weights.MonsterLoss, Later: true);
                    break;
                case Mine mine when !_ai.OwnTeam(owner, mine.Owner):
                    var mineGuard = mine.Guard is { } g ? _ai.Seen([new CreatureStack(g.Creature, Math.Max(1, g.Count ?? 1))], visit, null)[0] : null;
                    if (mineGuard is null || Plausible(_ai.Power(mineGuard)))
                    {
                        yield return new(visit, 8 * mine.DailyYield * AiContext.GoldValue(mine.Resource, 1), mineGuard is null ? null : Army.Of(mineGuard), Later: true);
                    }

                    break;
                case MonsterGuard monster when !hero.Army.IsEmpty:
                    var stack = _ai.SeenMonster(monster);
                    var monsterPower = _ai.Power(stack);
                    var joins = GuardService.JoinRatio(monster.Disposition) is { } ratio && power >= ratio * monsterPower && hero.Army.CanAccept(monster.Creature);
                    if (joins)
                    {
                        yield return new(visit, 150 + (monsterPower * 0.85));
                    }
                    else if (Plausible(monsterPower))
                    {
                        yield return new(visit, 150 + (monsterPower * _weights.GuardArmyShare) + OpensTheWay(owner, visit), Army.Of(stack), MaxLoss: _weights.MonsterLoss);
                    }

                    break;
                case CreatureDwelling { Guard: { } dwellingGuard } guardedDwelling when visit != hero.Position:
                    // Beat the guard to flag it (ADR-068); worth the dwelling plus the experience.
                    var guardStack = _ai.Seen([new CreatureStack(dwellingGuard.Creature, Math.Max(1, dwellingGuard.Count ?? 1))], visit, null)[0]!;
                    if (Plausible(_ai.Power(guardStack) + GuardPower(visit)))
                    {
                        yield return new(visit, 1200 + (_weights.GuardArmyShare * _ai.Power(guardStack)), Army.Of(guardStack), MaxLoss: _weights.MonsterLoss);
                    }

                    break;
                case CreatureDwelling dwelling when Plausible(GuardPower(visit)) && visit != hero.Position:
                    // Flag it (so caravans can hire there) and buy what waits, if the hero can pay and has room.
                    var dwelt = _state.Creatures.Get(dwelling.Creature);
                    var buyable = hero.Army.CanAccept(dwelt.Id)
                        ? TownRules.MaxAffordable(_state.GetPlayer(owner).Resources, dwelt.Cost, dwelling.Available ?? 0) : 0;
                    var worth = (_ai.OwnTeam(owner, dwelling.Owner) || _ai.Preparing ? 0 : 1200) + (0.4 * buyable * dwelt.FightValue);
                    if (worth > 0)
                    {
                        yield return new(visit, worth, GuardArmy(visit), MaxLoss: _weights.MonsterLoss);
                    }

                    break;
                // On an arena map's preparation day only neutral towns: an enemy's brings its heroes onto ours before the battle (ADR-160).
                // On an arena map's preparation day only neutral towns: an enemy's brings its heroes onto ours before the battle (ADR-160).
                case Town town when !_ai.OwnTeam(owner, town.Owner) && !(_ai.Preparing && town.Owner is not null):
                    var siege = SiegeRules.SpecOf(town);
                    var townWorth = _weights.TownValue + (town.Owner is null ? 0 : _weights.EnemyTownValue) + (town.Dwellings.Count * _weights.TownDwellingValue);
                    if (_state.HeroAt(visit) is { } gatekeeper)
                    {
                        // A hero on the entrance defends the town alone, behind its walls (ADR-106, ADR-112): that fight comes first, and is weighed
                        // with the town's worth; an unseen one is not known.
                        var keeper = _ai.Seen(gatekeeper);
                        if (_state.AreEnemies(owner, gatekeeper.Owner) && _state.CanSee(owner, gatekeeper) && Plausible(_ai.ArmyPower(keeper)))
                        {
                            yield return new(visit, townWorth + _weights.EnemyHeroValue + (_weights.HeroArmyShare * _ai.ArmyPower(keeper)), keeper, gatekeeper.Attack, gatekeeper.Defense,
                                Siege: gatekeeper.Owner == town.Owner ? siege : null);
                        }

                        break;
                    }

                    var defenders = AdventureIntel.Reckoned(AdventureIntel.ScoutTown(_state, town, owner).Garrison);
                    var garrison = _ai.ArmyPower(defenders);
                    if (Plausible(garrison))
                    {
                        yield return new(visit, townWorth + (0.3 * garrison), defenders.IsEmpty ? null : defenders, Siege: siege);
                    }

                    break;
                case Town own when own.Owner == owner && own.VisitPosition != hero.Position && GrailRules.Carries(_state, hero) && own.Level(BuiltInBuildings.Grail) == 0
                                   && TownRules.Buildings(_state, own).Contains(BuiltInBuildings.Grail):
                    // The Grail raises the town's Grail building (ADR-140).
                    yield return new(visit, _weights.GrailValue, Later: true);
                    break;
                case Town own when own.Owner == owner && own.VisitPosition != hero.Position && TownNeed(hero, own) is > 0 and var need:
                    yield return new(visit, need);
                    break;
                case TownGate when visit != hero.Position && GateTown(hero) is { Need: var gateNeed } && Plausible(GuardPower(visit)):
                    // The gate is worth (a little less than) the town it leads to; going there through the gate costs no movement (ADR-068).
                    yield return new(visit, 0.9 * gateNeed, GuardArmy(visit), MaxLoss: _weights.MonsterLoss, Later: true);
                    break;
                case HillFort when visit != hero.Position && HillFortGain(hero) is > 0 and var gain && Plausible(GuardPower(visit)):
                    yield return new(visit, gain, GuardArmy(visit), MaxLoss: _weights.MonsterLoss);
                    break;
                case WarMachineFactory when visit != hero.Position && FactoryWants(hero).Count is > 0 and var machines && Plausible(GuardPower(visit)):
                    yield return new(visit, 300 * machines, GuardArmy(visit), MaxLoss: _weights.MonsterLoss);
                    break;
                case SkillSchool school when visit != hero.Position && SchoolValue(hero, school) is > 0 and var schoolWorth && Plausible(GuardPower(visit)):
                    yield return new(visit, schoolWorth, GuardArmy(visit), MaxLoss: _weights.MonsterLoss);
                    break;
                case Obelisk obelisk when _state.Grail.Position is not null && !_state.Grail.IsFound && visit != hero.Position
                                          && !obelisk.WasVisitedBy(owner) && Plausible(GuardPower(visit)):
                    yield return new(visit, _weights.ObeliskValue, GuardArmy(visit), MaxLoss: _weights.MonsterLoss, Later: true);
                    break;
            }
        }

        if (GrailSpot(owner) is { } grail && grail != hero.Position && !hero.Artifacts.BackpackFull && Plausible(GuardPower(grail)))
        {
            yield return new(grail, _weights.GrailValue, GuardArmy(grail), MaxLoss: _weights.MonsterLoss, Later: true);
        }

        // A secondary hero carrying a worthwhile army brings it to the main hero (the meeting opens the exchange).
        if (hero.Id != _ai.Main && _ai.Main is { } mainId && _state.Heroes.FirstOrDefault(h => h.Id == mainId) is { } main
            && main.Position.Level == hero.Position.Level && (power >= _weights.CarryThreshold || power * 4 >= _ai.ArmyPower(main.Army)) && !main.Army.IsEmpty)
        {
            yield return new(main.Position, 0.5 * power);
        }

        // Not on an arena map's preparation day: beating the enemy's heroes before the battle leaves nobody to fight it, a loss for both (ADR-160).
        foreach (var enemy in _state.Heroes.Where(_ => !_ai.Preparing).OrderBy(h => h.Id.Value))
        {
            if (!_state.AreEnemies(owner, enemy.Owner) || !_state.CanSee(owner, enemy) || !_state.Fog.IsExplored(owner, enemy.Position) || Sanctuary.Protects(_state, enemy.Position))
            {
                continue;
            }

            var seen = _ai.Seen(enemy);
            var theirs = _ai.ArmyPower(seen);
            if (!hero.Army.IsEmpty && power * 10 >= theirs * 7)
            {
                // One standing in its own town defends it behind the walls (ADR-106).
                var home = _state.ObjectCellAt(enemy.Position) is { Object: Town town, Cell: FootprintCell.Visit } && town.Owner == enemy.Owner ? SiegeRules.SpecOf(town) : null;
                yield return new(enemy.Position, _weights.EnemyHeroValue + (_weights.HeroArmyShare * theirs), seen, enemy.Attack, enemy.Defense, Siege: home);
            }
        }
    }

    /// <summary>
    /// The cheapest explored tile next to unexplored ground that the hero can walk to, and the route's cost. A flood over open ground (no
    /// objects, heroes or guarded tiles: those are targets with a fight check) finds the tiles really reachable, nearest first, so ground
    /// seen over a wall does not hide the hero's own frontier; the route finder prices the first few.
    /// </summary>
    private (MapPosition Position, int Cost)? Explore(Hero hero, HashSet<MapPosition> failed)
    {
        var map = _state.Map;
        var owner = hero.Owner;
        var power = _ai.ArmyPower(hero.Army);
        var seen = new HashSet<MapPosition> { hero.Position };
        var queue = new Queue<MapPosition>();
        queue.Enqueue(hero.Position);
        var frontier = new List<MapPosition>();
        while (queue.Count > 0 && frontier.Count < _weights.FrontierChecked)
        {
            var position = queue.Dequeue();
            foreach (var direction in DirectionExtensions.All)
            {
                var next = position.Step(direction);
                if (!map.Contains(next) || !seen.Add(next))
                {
                    continue;
                }

                if (Teleport(next) is { } entrance)
                {
                    // A gate or portal: an unexplored exit is a place to explore (walking in takes the hero there), an explored one goes on.
                    if (_state.TeleportDestination(entrance) is not { } exit || !_state.Fog.IsExplored(owner, exit))
                    {
                        if (!failed.Contains(next) && !_ai.Claimed.Contains(next) && _threats.Holds(power, _threats.Danger(owner, next)))
                        {
                            frontier.Add(next);
                        }
                    }
                    else if (seen.Add(exit))
                    {
                        queue.Enqueue(exit);
                    }

                    continue;
                }

                if (!Open(next))
                {
                    continue;
                }

                queue.Enqueue(next);
                if (!failed.Contains(next) && !_ai.Claimed.Contains(next) && _threats.Holds(power, _threats.Danger(owner, next))
                    && DirectionExtensions.All.Any(d => map.Contains(next.Step(d)) && !_state.Fog.IsExplored(owner, next.Step(d))))
                {
                    frontier.Add(next);
                }
            }
        }

        (MapPosition Position, int Cost)? best = null;
        foreach (var position in frontier)
        {
            if (WalkCost(hero, position) is { } cost && (best is null || cost < best.Value.Cost))
            {
                best = (position, cost);
            }
        }

        return best;

        // Exploring never walks into anything: no heroes, objects or guarded ground; a border post it has the key for, an own or allied
        // garrison and a Quest Gate whose quest the hero meets are passes (ADR-070, ADR-071, EVT-6).
        bool Open(MapPosition position) =>
            _state.Fog.IsExplored(owner, position) && MovementRules.CheckTile(_state, position) == MoveResult.Moved
            && BorderRules.Barring(_state, owner, position) is null && _state.HeroAt(position) is null && !_state.IsGuarded(position)
            && _state.ObjectCellAt(position) switch
            {
                null => true,
                { Object: BorderPost, Cell: FootprintCell.Visit } => true,
                { Object: MapGarrison garrison, Cell: FootprintCell.Visit } => garrison.LetsThrough(_state, owner),
                { Object: QuestSite { Kind: MapObjectKind.QuestGate }, Cell: FootprintCell.Visit } => QuestRules.Barring(_state, hero, position) is null,
                _ => false,
            };

        MapObject? Teleport(MapPosition position) =>
            _state.Fog.IsExplored(owner, position) && !_state.IsGuarded(position) && _state.HeroAt(position) is null
            && _state.ObjectCellAt(position) is { Cell: FootprintCell.Visit, Object: { TeleportTarget: not null } gate } ? gate : null;
    }
}
