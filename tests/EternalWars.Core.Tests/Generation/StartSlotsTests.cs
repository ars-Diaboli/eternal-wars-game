using EternalWars.Core.Data.Maps;
using EternalWars.Core.Data.Records;
using EternalWars.Core.Data.Templates;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Generation;

namespace EternalWars.Core.Tests.Generation;

/// <summary>The new-game screen's random maps (ADR-086): a template's start slots, the heroes to pick, and a generated game that saves its seed.</summary>
public sealed class StartSlotsTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "ew-slots-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private static MapTemplate Duel() => BuiltInTemplates.Find("duel")!;

    [Fact]
    public void TheFirstSlotIsHuman_TheOthersComputers_AndTeamsAreTheStartZones()
    {
        var duel = StartSlots.Of(Duel());
        Assert.Equal([("Blue", true, true), ("Red", true, false)], duel.Select(s => (s.Name, s.HumanAllowed, s.HumanByDefault)));
        Assert.NotEqual(duel[0].Team, duel[1].Team); // free for all: a team each

        Assert.Equal([0, 0, 1, 1], StartSlots.Of(BuiltInTemplates.Find("Two against two")!).Select(s => s.Team));

        var aiFirst = Duel() with { Zones = [.. Duel().Zones.Select(z => z.Owner == 0 && z.Type == ZoneType.PlayerStart ? z with { Type = ZoneType.AiStart } : z)] };
        Assert.Equal([(false, false), (true, true)], StartSlots.Of(aiFirst).Select(s => (s.HumanAllowed, s.HumanByDefault)));
    }

    [Fact]
    public void MoreHumansThanTheTemplateAllows_AreRefused()
    {
        PlayerSetup Human(int slot) => new(slot, $"P{slot}", IsHuman: true, TownIds.Haven);

        Assert.Throws<ArgumentException>(() => MapGenerator.Build(Duel() with { MaxHumans = 1 }, 72, 1, [Human(0), Human(1)]));
        Assert.NotNull(MapGenerator.Build(Duel() with { MaxHumans = 1 }, 72, 1, [Human(0), Human(1) with { IsHuman = false }]).State);
    }

    [Fact]
    public void TheHeroesToPick_AreTheTownsByName()
    {
        var heroes = StartSlots.Heroes(HeroCatalog.Default, TownIds.Inferno);

        Assert.NotEmpty(heroes);
        Assert.All(heroes, h => Assert.Equal(TownIds.Inferno, h.Town));
        Assert.Equal(HeroCatalog.Default.All.Count(h => h.Town == TownIds.Inferno), heroes.Count);
        Assert.Equal(heroes.Select(h => h.Name).Order(StringComparer.Ordinal), heroes.Select(h => h.Name));
    }

    [Fact]
    public void AGameFromTheSlots_StartsAndItsSavesKeepHowTheMapWasMade()
    {
        var template = Duel();
        var slots = StartSlots.Of(template);
        var hero = StartSlots.Heroes(HeroCatalog.Default, TownIds.Necropolis)[0].Id;
        PlayerSetup[] players =
        [
            new(0, slots[0].Name, slots[0].HumanByDefault, TownIds.Necropolis, hero, slots[0].Team),
            new(1, slots[1].Name, slots[1].HumanByDefault, PlayerSetup.RandomTown, Team: slots[1].Team),
        ];

        var (state, generation) = MapGenerator.Build(template, 72, 11, players, new GameOptions(Seed: 11));
        Assert.Equal([true, false], state.Players.Select(p => p.IsHuman));
        Assert.Equal(hero, state.Heroes.Single(h => h.Owner == state.Players[0].Id).Definition);

        var initial = MapConverter.ToDocument(state);
        var session = new GameSession(state);
        session.Start();
        var generated = GenerationInfo.Of(template.Name, 72, 11, generation.Attempt);
        GameSaves.Autosave(_folder, initial, session, generated);
        var saved = GameSaves.Save(_folder, "random", initial, session, generated);

        Assert.Equal(generated, GameSaves.Load(saved).Record.Generated);
        Assert.Equal(generated, GameSaves.Load(GameSaves.PathFor(_folder, GameSaves.AutosaveCurrentName)).Record.Generated);
    }
}
