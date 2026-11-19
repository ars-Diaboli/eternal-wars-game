using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Players;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Events;

namespace EternalWars.Tools.Sim;

/// <summary>
/// What a sim battle adds to the two bare armies: both heroes casting the same few battle spells (ADR-188 e), and the defender in a Castle-level
/// town with the attacker's siege engine, aimed by its Basic Ballistics (ADR-188 f).
/// </summary>
internal sealed record FightSetup(bool Spells = false, bool Siege = false, int AttackerPercent = 100)
{
    public static FightSetup Plain { get; } = new();

    /// <summary>How many battle spells (levels 1–3, drawn by the battle's seed) both heroes know.</summary>
    public const int SpellCount = 5;

    public const int SpellPower = 3;

    public const int Knowledge = 4;
}

/// <summary>Single battles between two computer heroes, for the battle statistics and balance commands.</summary>
internal static class Fights
{
    /// <summary>
    /// One battle of copies of the two armies on a tiny map with <paramref name="seed"/>; the battle AI levels default to the latest. Every event goes to
    /// <paramref name="trace"/> when it is set.
    /// </summary>
    public static (BattleEnded Ended, int Rounds) FightOnce(
        SimContext sim, Army attacker, Army defender, ulong seed, (BattleAiLevel, BattleAiLevel)? levels = null, FightSetup? setup = null,
        Action<IEvent>? trace = null)
    {
        setup ??= FightSetup.Plain;

        // A siege: the defender stands at the entrance of its own town (7 × 7, the entrance at the bottom middle), the attacker below it.
        var (map, defenderAt, attackerAt, step) = setup.Siege
            ? (new AdventureMap(7, 8), new MapPosition(3, 6), new MapPosition(3, 7), Direction.North)
            : (new AdventureMap(3, 1), new MapPosition(1, 0), new MapPosition(0, 0), Direction.East);
        var players = new[] { new Player(new PlayerId(0), "Attacker", isHuman: false), new Player(new PlayerId(1), "Defender", isHuman: false) };
        var spells = setup.Spells ? SpellsFor(sim, seed) : null;
        var heroes = new[]
        {
            Hero(1, players[0].Id, attackerAt, Scaled(attacker, setup.AttackerPercent), spells, setup.Siege ? [WarMachineSlot.Offensive] : null),
            Hero(2, players[1].Id, defenderAt, defender, spells, null),
        };
        var objects = new List<MapObject>();
        if (setup.Siege)
        {
            var buildings = new Dictionary<EternalWars.Core.Domain.Common.BuildingId, int>(BuiltInBuildings.DefaultStart) { [BuiltInBuildings.Fort] = 3 };
            objects.Add(new Town(new MapObjectId(1), sim.Content.Objects.Get(BuiltInMapObjects.TownId(SimContext.PlayableTowns[0])), new MapPosition(0, 0),
                players[1].Id, buildings: buildings));
        }

        var session = new GameSession(new GameState(map, players, heroes, objects, options: new GameOptions(Seed: seed), content: sim.Content));
        BattleEnded? ended = null;
        var rounds = 0;
        session.Events.Subscribe<BattleEnded>(e => ended = e);
        session.Events.Subscribe<BattleRoundStarted>(e => rounds = e.Round);
        if (trace is not null)
        {
            session.Events.Subscribe<IEvent>(trace);
        }

        session.Battles.AiLevels = levels ?? (BattleAiLevel.Latest, BattleAiLevel.Latest);
        session.Start();
        session.Execute(new StepHeroCommand(players[0].Id, heroes[0].Id, step)).ThrowIfRejected();
        return (ended ?? throw new InvalidOperationException("The battle did not finish."), rounds);

        Hero Hero(int id, PlayerId owner, MapPosition at, Army army, IReadOnlyList<string>? known, IEnumerable<WarMachineSlot>? machines) =>
            Core.Domain.Heroes.Hero.Create(
                new HeroId(id), owner, at, new Army(army.Slots), definition: null, name: owner.Value == 0 ? "Attacker" : "Defender",
                spellPower: known is null ? null : FightSetup.SpellPower, knowledge: known is null ? null : FightSetup.Knowledge, warMachines: machines,
                spellBook: known is not null, spells: known?.Select(s => new EternalWars.Core.Domain.Common.SpellId(s)), spellCatalog: sim.Content.Spells,
                skills: machines is null ? null : new Dictionary<string, SkillLevel>(StringComparer.Ordinal) { [BuiltInSkills.Ballistics] = SkillLevel.Basic });
    }

    /// <summary>The army with every stack's count at <paramref name="percent"/> % (at least one creature).</summary>
    private static Army Scaled(Army army, int percent) =>
        percent == 100 ? army : new Army([.. army.Slots.Select(s => s is { } stack ? new CreatureStack(stack.Creature, Math.Max(1, stack.Count * percent / 100)) : s)]);

    /// <summary>The battle spells both heroes know in the battle with <paramref name="seed"/>: <see cref="FightSetup.SpellCount"/> of levels 1–3.</summary>
    private static List<string> SpellsFor(SimContext sim, ulong seed)
    {
        var pool = sim.Content.Spells.All.Where(s => s.Effect is not null && s.Level <= 3).OrderBy(s => s.Id).ToList();
        var random = new GameRandom(seed * 7919 + 17);
        var picked = new List<string>();
        while (picked.Count < FightSetup.SpellCount && pool.Count > 0)
        {
            var at = random.Next(pool.Count);
            picked.Add(pool[at].Id.Value);
            pool.RemoveAt(at);
        }

        return picked;
    }
}
