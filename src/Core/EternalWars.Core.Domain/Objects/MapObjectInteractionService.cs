using EternalWars.Core.Domain.Artifacts;
using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Decisions;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Towns;

namespace EternalWars.Core.Domain.Objects;

/// <summary>
/// What happens when a hero visits a map object or meets a hero. The objects that give something have their rules in
/// <see cref="MapObjectRewardVisits"/>; the answers to the decisions a visit opens are in <see cref="ObjectDecisionService"/>.
/// </summary>
public sealed class MapObjectInteractionService
{
    private readonly ObjectInteractionContext _context;
    private readonly BattleService _battles;
    private readonly GuardService _guards;
    private readonly ProgressionService _progression;
    private readonly GrailService _grail;
    private readonly MapObjectRewardVisits _rewards;
    private Dictionary<Type, Action<Hero, MapObject>>? _visitRules;

    internal MapObjectInteractionService(
        ObjectInteractionContext context,
        BattleService battles,
        GuardService guards,
        ProgressionService progression,
        ArtifactService artifacts,
        GrailService grail)
    {
        _context = context;
        _battles = battles;
        _guards = guards;
        _progression = progression;
        _grail = grail;
        _rewards = new MapObjectRewardVisits(context, battles, progression, artifacts);
    }

    private Game.GameState State => _context.State;

    private Core.Events.IEventPublisher Events => _context.Events;

    /// <inheritdoc cref="ObjectInteractionContext.TryCollect"/>
    internal bool TryCollect(Hero hero, MapObject mapObject) => _context.TryCollect(hero, mapObject);

    /// <summary>
    /// What a visit does, by instance type (ADR-166): every type of <see cref="MapObjectKinds"/> has a rule (<see cref="Nothing"/> for the objects
    /// used from a panel, or whose visit the move handles), and a test holds the two together.
    /// </summary>
    private Dictionary<Type, Action<Hero, MapObject>> VisitRules => _visitRules ??= new()
    {
        [typeof(ResourcePile)] = On<ResourcePile>(_rewards.CollectPile),
        [typeof(Mine)] = On<Mine>(VisitMine),
        [typeof(TreasureChest)] = On<TreasureChest>(_rewards.OpenChest),
        [typeof(TwoWayGate)] = Teleport,
        [typeof(OneWayGateEntrance)] = Teleport,
        [typeof(OneWayGateExit)] = Nothing,
        [typeof(MonsterGuard)] = On<MonsterGuard>((hero, monster) => _guards.Encounter(hero, monster)),
        [typeof(Town)] = On<Town>(VisitTown),
        [typeof(ArtifactPickup)] = On<ArtifactPickup>(_rewards.PickUp),
        [typeof(CreatureDwelling)] = On<CreatureDwelling>(VisitDwelling),
        [typeof(Marketplace)] = Nothing,
        [typeof(ResourceProvider)] = On<ResourceProvider>(VisitProvider),
        [typeof(LootSite)] = On<LootSite>(_rewards.Search),
        [typeof(BuffSite)] = On<BuffSite>(_rewards.Bless),
        [typeof(TrainingSite)] = On<TrainingSite>(_rewards.Train),
        [typeof(SignPost)] = On<SignPost>((hero, sign) => Events.Publish(new SignRead(hero.Id, sign.Id, sign.Message))),
        [typeof(Observatory)] = On<Observatory>(LookOut),
        [typeof(HillFort)] = Nothing,
        [typeof(TownGate)] = On<TownGate>(OfferTowns),
        [typeof(WarMachineFactory)] = Nothing,
        [typeof(Prison)] = On<Prison>(Free),
        [typeof(CreatureBank)] = On<CreatureBank>(_rewards.EnterBank),
        [typeof(DenOfThieves)] = On<DenOfThieves>((hero, den) => Events.Publish(new ThievesDenVisited(hero.Id, den.Id))),
        [typeof(AstrologersTower)] = On<AstrologersTower>((hero, tower) => Events.Publish(new AstrologerConsulted(hero.Id, tower.Id, State.NextWeekEvent))),
        [typeof(Sanctuary)] = Nothing,
        [typeof(Scenery)] = Nothing,
        [typeof(Cartographer)] = On<Cartographer>(_rewards.OfferMap),
        [typeof(ArtifactMerchant)] = Nothing,
        [typeof(RefugeeCamp)] = Nothing,
        [typeof(SacrificialAltar)] = Nothing,
        [typeof(Sphinx)] = On<Sphinx>(_rewards.AskRiddle),
        [typeof(WitchHut)] = On<WitchHut>(_rewards.OfferSkills),
        [typeof(Shrine)] = On<Shrine>(_rewards.Worship),
        [typeof(SkillSchool)] = On<SkillSchool>(_rewards.DrawLessons),
        [typeof(Obelisk)] = On<Obelisk>(_grail.VisitObelisk),
        [typeof(CombatPlace)] = Nothing,
        [typeof(Deposit)] = On<Deposit>(_rewards.OfferDeposit),
        [typeof(MagiWindow)] = On<MagiWindow>(LookThrough),
        [typeof(MagiEye)] = Nothing,
        [typeof(KeymasterTent)] = On<KeymasterTent>(VisitKeymaster),
        [typeof(BorderPost)] = On<BorderPost>(PassBorderGuard),
        [typeof(MapTavern)] = Nothing,
        [typeof(MapGarrison)] = On<MapGarrison>(VisitGarrison),
        [typeof(PandorasBox)] = On<PandorasBox>(_rewards.OpenBox),

        // The move runs the quest (MapEventService.VisitQuest), not the visit.
        [typeof(QuestSite)] = Nothing,
    };

    private static readonly Action<Hero, MapObject> Nothing = (_, _) => { };

    private static Action<Hero, MapObject> On<T>(Action<Hero, T> rule)
        where T : MapObject => (hero, mapObject) => rule(hero, (T)mapObject);

    /// <summary>Whether visiting an object of the type has a rule (every <see cref="MapObjectKinds.Types"/> entry must).</summary>
    internal bool HasVisitRule(Type type) => VisitRules.ContainsKey(type);

    /// <summary>A hero steps onto the object's entrance: <see cref="MapObjectVisited"/>, then what its kind does.</summary>
    internal void Visit(Hero hero, MapObject mapObject)
    {
        var rule = VisitRules.GetValueOrDefault(mapObject.GetType())
                   ?? throw new InvalidOperationException($"{mapObject.GetType().Name} has no visit rule (ADR-166).");
        Events.Publish(new MapObjectVisited(hero.Id, mapObject.Id));
        rule(hero, mapObject);
    }

    internal void Meet(Hero hero, Hero other)
    {
        var decision = new HeroExchangeDecision(hero.Id, other.Id);
        State.PendingDecision = decision;
        Events.Publish(new HeroesMet(hero.Id, other.Id));
        Events.Publish(new DecisionRequested(decision));
    }

    /// <summary>A hero attacks an enemy hero: <see cref="BattleTriggered"/>, then the battle itself.</summary>
    internal void StartBattle(Hero attacker, Hero defender, bool isAmbush)
    {
        Events.Publish(new BattleTriggered(attacker.Id, defender.Id, isAmbush));
        _battles.BeginHeroBattle(attacker, defender, isAmbush);
    }

    private void VisitMine(Hero hero, Mine mine)
    {
        if (mine.Guard is not null)
        {
            _guards.Encounter(hero, mine);
        }
        else if (mine.Owner != hero.Owner)
        {
            var previous = mine.Owner;
            mine.Owner = hero.Owner;
            Events.Publish(new MineOwnerChanged(mine.Id, previous, hero.Owner));
        }
    }

    private void VisitDwelling(Hero hero, CreatureDwelling dwelling)
    {
        if (dwelling.Guard is not null)
        {
            _guards.Encounter(hero, dwelling);
        }
        else if (dwelling.Owner != hero.Owner)
        {
            var before = dwelling.Owner;
            dwelling.Owner = hero.Owner;
            Events.Publish(new DwellingOwnerChanged(dwelling.Id, before, hero.Owner));
        }
    }

    private void VisitProvider(Hero hero, ResourceProvider provider)
    {
        if (provider.Owner != hero.Owner)
        {
            Flag(hero, provider);
        }
        else if (provider.Stored.IsEmpty)
        {
            Events.Publish(new MapObjectFoundEmpty(hero.Id, provider.Id));
        }
        else
        {
            var stored = provider.Stored;
            provider.Stored = ResourceSet.Empty;
            Events.Publish(new ProviderStockChanged(provider.Id, ResourceSet.Empty));
            _context.GiveResources(hero, provider, stored);
        }
    }

    /// <summary>
    /// A hero flags a provider (ADR-079, TOWN-9): the first flagging of a neutral one brings a week's output at once; otherwise the hero takes
    /// whatever its former owner had left there.
    /// </summary>
    private void Flag(Hero hero, ResourceProvider provider)
    {
        var previous = provider.Owner;
        var taken = provider.WasClaimed ? provider.Stored : ResourceProviderRules.Roll(provider.Definition, State.Date, State.Random);
        provider.Owner = hero.Owner;
        provider.WasClaimed = true;
        Events.Publish(new ProviderOwnerChanged(provider.Id, previous, hero.Owner));
        if (!provider.Stored.IsEmpty)
        {
            provider.Stored = ResourceSet.Empty;
            Events.Publish(new ProviderStockChanged(provider.Id, ResourceSet.Empty));
        }

        if (!taken.IsEmpty)
        {
            _context.GiveResources(hero, provider, taken);
        }
    }

    private void VisitTown(Hero hero, Town town)
    {
        if (town.Owner is { } owner && !State.AreEnemies(hero.Owner, owner))
        {
            // Own or allied towns: the hero just stands in the town (an own hero can take recruits and garrison stacks).
            TownService.AutoAppoint(State, Events, town, hero);
            Magic.MagicRules.Visit(State, Events, hero, town);
        }
        else if (!town.Garrison.IsEmpty || town.GarrisonHero is not null)
        {
            _battles.BeginTownBattle(hero, town);
        }
        else
        {
            TownService.Capture(State, Events, town, hero.Owner);
            Magic.MagicRules.Visit(State, Events, hero, town);
        }
    }

    private void Teleport(Hero hero, MapObject gate)
    {
        // Two-way partner or one-way exit; a hero standing on the landing tile blocks the jump.
        if (gate.TeleportTarget is not null && State.TeleportDestination(gate) is { } exit && State.HeroAt(exit) is null)
        {
            var from = hero.Position;
            hero.Position = exit;
            Events.Publish(new HeroTeleported(hero.Id, from, exit));
        }
    }

    /// <summary>The town gate's picker: the owner's towns, an occupied entrance listed but unavailable (ADR-068, MAP-12).</summary>
    private void OfferTowns(Hero hero, TownGate gate)
    {
        var towns = State.ObjectsOf<Town>().Where(t => t.Owner == hero.Owner).OrderBy(t => t.Id.Value)
            .Select(t => new TownGateOption(t.Id, t.Name, State.HeroAt(t.VisitPosition) is null))
            .ToList();
        if (towns.Count == 0)
        {
            Events.Publish(new MapObjectRefused(hero.Id, gate.Id, "You have no town to go to."));
            return;
        }

        _context.Ask(new TownGateDecision(hero.Id, gate.Id, towns));
    }

    /// <summary>A prisoner joins the visitor's player where the prison stood, unless the player has all the heroes it may (ADR-068, MAP-5).</summary>
    private void Free(Hero hero, Prison prison)
    {
        if (State.HeroLimitReached(hero.Owner))
        {
            Events.Publish(new MapObjectRefused(hero.Id, prison.Id, $"You may have at most {State.HeroLimit(hero.Owner)} heroes; the prisoner stays."));
            return;
        }

        if (prison.Hero is not { } heroId || !State.HeroRoster.Contains(heroId))
        {
            Events.Publish(new MapObjectRefused(hero.Id, prison.Id, "The prison is empty."));
            return;
        }

        _context.RemoveFromMap(prison);
        var level = Math.Max(1, prison.Level ?? 1);
        var freed = Hero.Create(State.NextUnusedHeroId(), hero.Owner, prison.Position, null, State.HeroRoster.Get(heroId));
        freed.Experience = ExperienceTable.For(level);
        State.AddHero(freed);
        _progression.LevelUpAutomatically(freed);
        freed.MaxMovementPoints = MovementAllowance.Daily(State, freed);
        freed.MovementPoints = 0; // it starts moving on its owner's next turn
        Events.Publish(new HeroFreed(prison.Id, freed.Id, hero.Owner, heroId, freed.Level));
    }

    private void LookOut(Hero hero, Observatory observatory)
    {
        // Every visit looks out (it only reveals what is still hidden); the log tells the AI which heroes have been up there.
        _context.TryCollect(hero, observatory);
        Events.Publish(new SurroundingsRevealed(hero.Id, observatory.Id, hero.Owner, observatory.VisitPosition, Observatory.Radius));
    }

    private void LookThrough(Hero hero, MagiWindow window)
    {
        // Every visit looks through every Eye of the window's colour (ADR-073).
        foreach (var eye in State.ObjectsOf<MagiEye>().Where(e => e.Colour == window.Colour).OrderBy(e => e.Id.Value))
        {
            Events.Publish(new SurroundingsRevealed(hero.Id, window.Id, hero.Owner, eye.Position, MagiWindow.Radius));
        }
    }

    private void VisitKeymaster(Hero hero, KeymasterTent tent)
    {
        if (State.GetPlayer(hero.Owner).AddKey(tent.Colour))
        {
            Events.Publish(new KeyReceived(tent.Id, hero.Id, hero.Owner, tent.Colour));
        }
        else
        {
            Events.Publish(new MapObjectFoundEmpty(hero.Id, tent.Id));
        }
    }

    private void PassBorderGuard(Hero hero, BorderPost post)
    {
        // Only key holders get onto a border post (the move refuses the others); a guard leaves after the first, a gate stays.
        if (!post.IsGate)
        {
            _context.RemoveFromMap(post);
            Events.Publish(new BorderGuardPassed(post.Id, hero.Id));
        }
    }

    private void VisitGarrison(Hero hero, MapGarrison garrison)
    {
        if (garrison.LetsThrough(State, hero.Owner))
        {
            return;
        }

        if (!garrison.Troops.IsEmpty)
        {
            _battles.BeginGarrisonBattle(hero, garrison);
        }
        else if (garrison.Owner != hero.Owner)
        {
            // An empty garrison is flagged by the first hero of another side that enters it (ADR-070).
            var previous = garrison.Owner;
            garrison.Owner = hero.Owner;
            Events.Publish(new GarrisonOwnerChanged(garrison.Id, previous, hero.Owner));
        }
    }
}
