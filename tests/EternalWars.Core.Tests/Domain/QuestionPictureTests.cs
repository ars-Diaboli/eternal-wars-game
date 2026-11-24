using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Decisions;
using EternalWars.Core.Domain.Objects;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>The picture over each kind of question card (ADR-309), named as the question picture prompts name the files.</summary>
public class QuestionPictureTests
{
    private static readonly HeroId Hero = new(1);
    private static readonly MapObjectId Thing = new(1);

    [Fact]
    public void EachQuestion_HasItsPicture()
    {
        var state = Create("A....", ".....");
        Start(state);

        Assert.Equal("q_treasure-chest", QuestionPictures.Of(state, new TreasureChestDecision(Hero, Thing, 1000, 500)));
        Assert.Equal("q_deposit", QuestionPictures.Of(state, new DepositDecision(Hero, Thing, default)));
        Assert.Equal("q_cartographer", QuestionPictures.Of(state, new CartographerDecision(Hero, Thing, 0, 1000)));
        Assert.Equal("q_sphinx", QuestionPictures.Of(state, new SphinxDecision(Hero, Thing, "?", ["a", "b"], 0)));
        Assert.Equal("q_pandoras-box", QuestionPictures.Of(state, new PandorasBoxDecision(Hero, Thing)));
        Assert.Equal("q_witch-hut", QuestionPictures.Of(state, new WitchHutDecision(Hero, Thing, ["logistics"])));
        Assert.Equal("q_town-gate", QuestionPictures.Of(state, new TownGateDecision(Hero, Thing, [])));
        Assert.Equal("q_guard-join", QuestionPictures.Of(state, new GuardEncounterDecision(Hero, Thing, GuardOffer.Join, new CreatureId("pikeman"), 5)));
        Assert.Equal("q_guard-flee", QuestionPictures.Of(state, new GuardEncounterDecision(Hero, Thing, GuardOffer.Flee, new CreatureId("pikeman"), 5)));
        Assert.Equal("q_level-up", QuestionPictures.Of(state, new LevelUpDecision(Hero, 2, ["logistics"])));
        Assert.Equal("q_event-army", QuestionPictures.Of(state, new MapEventArmyDecision(Hero, [new CreatureStack(new CreatureId("pikeman"), 1)])));
    }

    [Fact]
    public void AMapEvent_ShowsTheRewardPicture_OnlyForAMessageThatGivesSomething()
    {
        var state = Create("A....", ".....");
        Start(state);

        Assert.Equal("q_event-reward", QuestionPictures.Of(state, new MapEventDecision(Hero, "e", "A gift.", ["+500 gold"], [])));
        Assert.Equal("q_event-message", QuestionPictures.Of(state, new MapEventDecision(Hero, "e", "Hello.", [], [])));
        Assert.Equal("q_event-message", QuestionPictures.Of(state, new MapEventDecision(Hero, "e", "Pay?", ["+500 gold"], ["Yes", "No"])));
    }

    [Fact]
    public void ATrainingSite_ShowsItsOwnPicture_AndTheBattleAndTheExchangeNone()
    {
        var state = Create("A..........", "...........", "...........", "...........", "...........");
        var arena = Add<TrainingSite>(state, BuiltInMapObjects.Arena, 2, 0);
        var colosseum = Add<TrainingSite>(state, BuiltInMapObjects.ColosseumOfTheMagi, 5, 0);
        var stone = Add<TrainingSite>(state, BuiltInMapObjects.LearningStone, 8, 0);
        var gazebo = Add<TrainingSite>(state, BuiltInMapObjects.Gazebo, 2, 3);
        var tree = Add<TrainingSite>(state, BuiltInMapObjects.TreeOfKnowledge, 6, 3);
        Start(state);

        Assert.Equal("q_arena", QuestionPictures.Of(state, new TrainingDecision(Hero, arena.Id, [TrainingOption.Attack], false)));
        Assert.Equal("q_colosseum", QuestionPictures.Of(state, new TrainingDecision(Hero, colosseum.Id, [TrainingOption.SpellPower], false)));
        Assert.Equal("q_gazebo", QuestionPictures.Of(state, new TrainingDecision(Hero, gazebo.Id, [TrainingOption.PayGold], true)));
        Assert.Equal("q_tree-of-knowledge", QuestionPictures.Of(state, new TrainingDecision(Hero, tree.Id, [TrainingOption.PayGold], true)));
        Assert.Null(QuestionPictures.Of(state, new TrainingDecision(Hero, stone.Id, [], false)));
        Assert.Null(QuestionPictures.Of(state, new BattleDecision(Hero)));
        Assert.Null(QuestionPictures.Of(state, new HeroExchangeDecision(Hero, new HeroId(2))));
    }
}
