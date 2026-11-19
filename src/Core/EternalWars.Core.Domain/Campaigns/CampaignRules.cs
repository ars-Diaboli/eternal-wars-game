using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Players;

namespace EternalWars.Core.Domain.Campaigns;

/// <summary>
/// The campaign's rules (ADR-241): the options a campaign map starts with and what a won map carries on; its carried heroes are placed by
/// <see cref="CarriedHeroService"/>.
/// </summary>
public static class CampaignRules
{
    /// <summary>A new campaign at its first map.</summary>
    public static CampaignProgress Start(Difficulty difficulty) => new(0, difficulty);

    /// <summary>
    /// The options of the campaign's next map: its difficulty, the variables and heroes carried in, and <paramref name="seed"/>. The campaign
    /// variables are set even when none are carried, which is how a game knows it is a campaign map (<see cref="InCampaign"/>).
    /// </summary>
    public static GameOptions Options(CampaignProgress progress, ulong seed) =>
        new(Seed: seed, Difficulty: progress.Difficulty) { CampaignVariables = progress.Variables, CarriedHeroes = progress.Heroes };

    /// <summary>Whether the game is a campaign map: never ranked (<see cref="ScoreRules.IsRanked"/>).</summary>
    public static bool InCampaign(GameOptions options) => options.CampaignVariables is not null || options.CarriedHeroes is not null;

    /// <summary>The person playing the campaign: the map's first human player (a campaign map is for one person), or null.</summary>
    public static Player? PlayerOf(GameState state) => state.Players.Where(p => p.IsHuman).OrderBy(p => p.Id.Value).FirstOrDefault();

    /// <summary>
    /// What a won map carries on: the next map's index, the days it took, the variables (the map's kept ones over those carried so far) and the
    /// heroes the next map takes in (<see cref="CampaignMap.CarryHeroes"/>, then <see cref="CampaignMap.CarryStrongest"/>). Null when the campaign's
    /// person did not win.
    /// </summary>
    public static CampaignProgress? Finish(Campaign campaign, CampaignProgress progress, GameState state)
    {
        if (PlayerOf(state) is not { } player || state.Outcome is not { } outcome || !outcome.Winners.Contains(player.Id))
        {
            return null;
        }

        var variables = new Dictionary<string, int>(progress.Variables, StringComparer.Ordinal);
        foreach (var (name, value) in state.MapEvents.CampaignVariables)
        {
            variables[name] = value;
        }

        var next = progress.Map + 1;
        return progress with
        {
            Map = next,
            Variables = variables,
            Days = [.. progress.Days, state.Date.AbsoluteDay],
            Heroes = next < campaign.Maps.Count ? Carried(campaign.Maps[next], state, player) : [],
        };
    }

    /// <summary>The heroes the next map takes in, in its order: the named ones the player has, then the strongest others.</summary>
    private static IReadOnlyList<CarriedHero> Carried(CampaignMap next, GameState state, Player player)
    {
        var playing = state.AllHeroesOf(player.Id).Where(h => h.Definition is not null)
            .OrderByDescending(h => h.Level).ThenByDescending(h => h.Experience).ThenBy(h => h.Id.Value)
            .Select(h => Capture(h, next.KeepArmy, next.KeepArtifacts))
            .ToList();
        var candidates = playing
            .Concat(state.CarriedWaiting.Where(w => playing.All(p => p.Hero != w.Hero)).Select(w => Trimmed(w, next)))
            .ToList();
        var named = next.CarryHeroes.Select(id => candidates.FirstOrDefault(c => c.Hero == id)).OfType<CarriedHero>().ToList();
        return [.. named, .. candidates.Except(named).Take(Math.Max(0, next.CarryStrongest))];
    }

    private static CarriedHero Trimmed(CarriedHero hero, CampaignMap next) => hero with
    {
        Army = next.KeepArmy ? hero.Army : null,
        Artifacts = next.KeepArtifacts ? hero.Artifacts : null,
        Backpack = next.KeepArtifacts ? hero.Backpack : null,
    };

    /// <summary>The hero as the campaign carries it; its army and artifacts only when asked.</summary>
    public static CarriedHero Capture(Hero hero, bool army, bool artifacts) =>
        new(hero.Definition ?? throw new ArgumentException("Only a roster hero is carried.", nameof(hero)), hero.Name)
        {
            Level = hero.Level,
            Experience = hero.Experience,
            Attack = hero.Attack,
            Defense = hero.Defense,
            SpellPower = hero.SpellPower,
            Knowledge = hero.Knowledge,
            Skills = hero.Skills.Levels.ToDictionary(StringComparer.Ordinal),
            SpellBook = hero.HasSpellBook,
            Spells = hero.Spells.ToDictionary(StringComparer.Ordinal),
            WarMachines = [.. hero.WarMachines.Order()],
            Artifacts = artifacts ? [.. hero.Artifacts.AllWorn.Select(w => w.Id)] : null,
            Backpack = artifacts ? [.. hero.Artifacts.Backpack] : null,
            Army = army ? [.. hero.Army.Stacks.Select(s => new CarriedStack(s.Slot, s.Stack.Creature, s.Stack.Count))] : null,
        };
}
