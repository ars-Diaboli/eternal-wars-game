using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Objects;

namespace EternalWars.Core.Domain.Decisions;

/// <summary>
/// The picture over a question card (ADR-309): one per kind of question, named as the question picture prompts name the files
/// (<c>q_treasure-chest</c>, ...). The battle and the hero exchange open screens of their own and have none; nor have the training
/// sites that ask nothing.
/// </summary>
public static class QuestionPictures
{
    /// <summary>The picture's file name without its extension, or null for a question with none.</summary>
    public static string? Of(GameState state, PendingDecision decision)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(decision);
        return decision switch
        {
            TreasureChestDecision => "q_treasure-chest",
            TrainingDecision training => (state.FindObject(training.Site) as TrainingSite)?.Rule switch
            {
                TrainingRule.Arena => "q_arena",
                TrainingRule.ColosseumOfTheMagi => "q_colosseum",
                TrainingRule.Gazebo => "q_gazebo",
                TrainingRule.TreeOfKnowledge => "q_tree-of-knowledge",
                _ => null,
            },
            DepositDecision => "q_deposit",
            CartographerDecision => "q_cartographer",
            SphinxDecision => "q_sphinx",
            PandorasBoxDecision => "q_pandoras-box",
            WitchHutDecision => "q_witch-hut",
            TownGateDecision => "q_town-gate",
            GuardEncounterDecision { Offer: GuardOffer.Join } => "q_guard-join",
            GuardEncounterDecision => "q_guard-flee",
            LevelUpDecision => "q_level-up",
            MapEventArmyDecision => "q_event-army",
            MapEventDecision { IsQuestion: false, Dialogue.Count: 0, Lines.Count: > 0 } => "q_event-reward",
            MapEventDecision => "q_event-message",
            _ => null,
        };
    }
}
