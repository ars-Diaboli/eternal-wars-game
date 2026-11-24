using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Turns;
using EternalWars.Core.Domain.Visibility;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>The adventure HUD's turn banner, the turn timer's words, the minimap's pictograms and the level names (ADR-309).</summary>
public class HudPartsTests
{
    private static readonly string[] Field =
    [
        "A.........",
        "..........",
        "..........",
        "..........",
        "..........",
        "..........",
        "..........",
        "..........",
        ".........B",
    ];

    [Fact]
    public void WithTwoPeoplePlaying_TheBannerStays_ForEveryTurn()
    {
        var state = Create(Field);
        Start(state);

        var banner = TurnBanner.Of(state)!;

        Assert.Equal("Blue's turn", banner.Text);
        Assert.Equal(Blue, banner.Player);
        Assert.True(banner.Lasting);
    }

    [Fact]
    public void WithOnePersonPlaying_TheBannerIsBrief_AndNotShownInAComputersTurn()
    {
        var state = Create(Field);
        state.GetPlayer(Red).IsHuman = false;
        var session = Start(state);

        var banner = TurnBanner.Of(state)!;
        Assert.False(banner.Lasting);

        session.Execute(new Core.Domain.Commands.EndTurnCommand(Blue));
        Assert.Equal(Red, state.CurrentPlayer.Id);
        Assert.Null(TurnBanner.Of(state));
    }

    [Fact]
    public void TheBannersCrest_IsThePlayersFirstTown_ElseATownItOwns_ElseNone()
    {
        var state = Create(Field);
        Start(state);
        var blue = state.GetPlayer(Blue);

        Assert.Null(TurnBanner.Of(state)!.Crest);

        AddTown(state, "sylvan", 0, 0, Blue);
        Assert.Equal("sylvan", TurnBanner.CrestOf(state, blue));

        blue.StartTown = "haven";
        Assert.Equal("haven", TurnBanner.Of(state)!.Crest);
    }

    [Fact]
    public void NoBanner_BeforeTheGameStarts()
    {
        Assert.Null(TurnBanner.Of(Create(Field)));
    }

    [Theory]
    [InlineData(125.0, "2:05")]
    [InlineData(60.0, "1:00")]
    [InlineData(29.2, "0:30")] // rounded up
    [InlineData(0.0, "0:00")]
    public void TheClock_ShowsMinutesAndSeconds(double left, string shown) => Assert.Equal(shown, TurnClockText.Time(left));

    [Fact]
    public void ThePill_TurnsRedForTheLastMinute_NotWhileStopped()
    {
        Assert.False(TurnClockText.IsRed(61, stopped: false, up: false));
        Assert.True(TurnClockText.IsRed(60, stopped: false, up: false));
        Assert.False(TurnClockText.IsRed(30, stopped: true, up: false));
        Assert.True(TurnClockText.IsRed(0, stopped: false, up: true));
        Assert.Equal("0:30 stopped during the battle", TurnClockText.Pill(30, stopped: true, up: false));
        Assert.Equal("Time is up: the turn ends as soon as it can", TurnClockText.Pill(0, stopped: false, up: true));
    }

    [Fact]
    public void EveryWarning_HasItsWords()
    {
        Assert.Equal(
            ["1 minute left!", "30 seconds left!", "15 seconds left!", "5 seconds left!"],
            TurnTimer.Warnings.Select(TurnClockText.Warning));
        Assert.Equal("2 minutes left!", TurnClockText.Warning(120));
    }

    [Fact]
    public void TheMinimap_DrawsTowns_InTheirMiddle_AndTheHeroesTheViewerSees()
    {
        var state = Create(Field);
        Start(state);
        var town = AddTown(state, "haven", 1, 1, Red);
        var cells = KnownObjectCells(town);

        var all = Minimap.Pictograms(state, null, 0);

        var drawn = Assert.Single(all, p => p.Kind == MinimapPictogramKind.Town);
        Assert.Equal((cells.Average(c => c.X), cells.Average(c => c.Y)), (drawn.X, drawn.Y));
        Assert.Equal(Red, drawn.Owner);
        Assert.Equal([(0.0, 0.0, Blue), (9.0, 8.0, Red)], all.Where(p => p.Kind == MinimapPictogramKind.Hero).Select(p => (p.X, p.Y, p.Owner!.Value)));
        Assert.Equal(MinimapPictogramKind.Town, all[0].Kind); // towns first, the heroes drawn over them
        Assert.Empty(Minimap.Pictograms(state, null, 1));
    }

    [Fact]
    public void TheMinimap_LeavesOut_ATownTheViewerNeverSaw()
    {
        var state = Create(Field);
        Start(state, revealAll: false);
        AddTown(state, "haven", 2, 1, Red);

        Assert.DoesNotContain(Minimap.Pictograms(state, Blue, 0), p => p.Kind == MinimapPictogramKind.Town);
    }

    [Fact]
    public void TheMinimap_ShowsTheViewersOwnHeroes_ButNotAnEnemyHeroOutOfSight()
    {
        var state = Create(Field);
        Start(state, revealAll: false);

        var heroes = Minimap.Pictograms(state, Blue, 0).Where(p => p.Kind == MinimapPictogramKind.Hero).ToList();

        Assert.Equal([Blue], heroes.Select(p => p.Owner!.Value)); // Red's hero stands in the far corner, unseen
    }

    [Fact]
    public void TheLevels_AreNamed() => Assert.Equal(["Surface", "Underground"], new[] { 0, 1 }.Select(MapLevels.Name));

    private static List<Core.Domain.Common.MapPosition> KnownObjectCells(Core.Domain.Towns.Town town) =>
        [.. KnownObject.Of(town).Cells()];
}
