using EternalWars.Core.Data.Maps;
using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Decisions;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Events;
using EternalWars.Core.Network;

namespace EternalWars.Core.Tests.Network;

/// <summary>The pieces of a game a LAN host sends cross the wire whole (ADR-193): written, read back and written again, they read the same.</summary>
public class StateCodecTests
{
    public static TheoryData<string> SmokeMaps() =>
        [.. Directory.GetFiles(SmokeFolder(), "*.ewmap.json").Select(Path.GetFileName).OfType<string>().Order()];

    [Theory]
    [MemberData(nameof(SmokeMaps))]
    public void EveryHeroAndObjectOfAStartedGame_ReadsBackTheSame(string map)
    {
        var session = new GameSession(MapConverter.ToGameState(MapSerializer.Load(Path.Combine(SmokeFolder(), map))));
        session.Start();
        var state = session.State;

        foreach (var hero in state.Heroes.Concat(state.GarrisonHeroes))
        {
            RoundTrip(state, hero);
        }

        foreach (var mapObject in state.Objects)
        {
            var back = RoundTrip(state, mapObject);
            Assert.Equal(mapObject.GetType(), back.GetType());
            Assert.Same(mapObject.Definition, back.Definition);
        }

        foreach (var known in state.Players.SelectMany(p => state.Memory.Of(p.Id)))
        {
            Assert.Equal(known, RoundTrip(state, known));
        }
    }

    [Fact]
    public void ASiege_TheBattleAndItsQuestion_ReadBackTheSame()
    {
        var session = new GameSession(MapConverter.ToGameState(MapSerializer.Load(Path.Combine(SmokeFolder(), "siege.ewmap.json"))));
        session.Start();
        var events = new List<IEvent>();
        session.Events.Subscribe<IEvent>(events.Add);
        var attacker = session.State.HeroesOf(session.State.CurrentPlayer.Id).First();
        var town = session.State.ObjectsOf<EternalWars.Core.Domain.Towns.Town>().First(t => t.Owner != attacker.Owner);
        session.Execute(new MoveHeroCommand(attacker.Owner, attacker.Id, town.VisitPosition)).ThrowIfRejected();

        Assert.NotNull(session.State.Battle);
        RoundTrip(session.State, session.State.Battle);
        RoundTrip<PendingDecision>(session.State, session.State.PendingDecision!);
        Assert.All(events, e => Assert.Equal(e.GetType(), RoundTrip<IEvent>(session.State, e).GetType()));
    }

    [Fact]
    public void Write_NamesAFieldAlsoInABaseType_ByItsType_AndWritesStructsAndDictionaryEntries()
    {
        var codec = new StateCodec(new GameSession(MapConverter.ToGameState(MapSerializer.Load(Path.Combine(SmokeFolder(), "siege.ewmap.json")))).State);

        var written = codec.Write(new Derived(1, 2, new Spot(3, 4), new Dictionary<string, int> { ["a"] = 5, ["b"] = 6 }))!.ToJsonString();

        Assert.Equal("""{"Base.count":1,"Derived.count":2,"spot":{"X":3,"Y":4},"entries":[["a",5],["b",6]]}""", written);
    }

    private class Base(int count)
    {
#pragma warning disable CS9124 // the parameter is kept in a field on purpose: the test needs a base field of the same name
        private readonly int count = count;
#pragma warning restore CS9124

        public int BaseCount => count;
    }

    private sealed class Derived(int baseCount, int count, Spot spot, Dictionary<string, int> entries) : Base(baseCount)
    {
        private readonly int count = count;
        private readonly Spot spot = spot;
        private readonly Dictionary<string, int> entries = entries;

        public override string ToString() => $"{count} {spot} {entries.Count}";
    }

    private readonly struct Spot(int x, int y)
    {
        public readonly int X = x;
        public readonly int Y = y;
    }

    private static T RoundTrip<T>(GameState state, T value)
    {
        var codec = new StateCodec(state);
        var written = codec.Write(value);
        var back = codec.Read<T>(System.Text.Json.Nodes.JsonNode.Parse(written!.ToJsonString()))!;
        Assert.Equal(written.ToJsonString(), codec.Write(back)!.ToJsonString());
        return back;
    }

    private static string SmokeFolder()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "EternalWars.sln")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found."), "tools", "smoke");
    }
}
