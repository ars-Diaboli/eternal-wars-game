using EternalWars.Core.Data.Maps;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Decisions;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Scenarios;
using EternalWars.Core.Domain.Turns;

namespace EternalWars.Tools.Sim;

/// <summary>The demo scenario played by scripted heroes (<c>record</c>, <c>selftest</c>).</summary>
internal static class DemoGame
{
    /// <summary>Every day each hero heads for the enemy's start; chests give gold, guards are fought, battles quick-resolved.</summary>
    public static (MapDocument Map, GameSession Session) Play(SimContext sim, int days, ulong seed)
    {
        var map = MapConverter.ToDocument(DemoScenario.Create());
        var session = new GameSession(MapConverter.ToGameState(map, sim.Content, new GameOptions(Seed: seed)));
        var state = session.State;
        session.Start();
        for (var turn = 0; turn < days * state.Players.Count; turn++)
        {
            var player = state.CurrentPlayer.Id;
            var goal = player.Value == 0 ? new MapPosition(state.Map.Width - 3, state.Map.Height - 3) : new MapPosition(2, 2);
            foreach (var hero in state.HeroesOf(player).ToList())
            {
                if (state.Heroes.Contains(hero) && Waypoint(session, hero, goal) is { } waypoint)
                {
                    session.Execute(new MoveHeroCommand(player, hero.Id, waypoint));
                    Settle(session);
                }
            }

            session.Execute(new EndTurnCommand(state.CurrentPlayer.Id)).ThrowIfRejected();
        }

        return (map, session);
    }

    /// <summary>The explored tile closest to <paramref name="goal"/> that the hero has a route to (routes avoid unexplored tiles).</summary>
    private static MapPosition? Waypoint(GameSession session, Hero hero, MapPosition goal)
    {
        var state = session.State;
        return state.Map.Positions(hero.Position.Level)
            .Where(p => p != hero.Position && state.Fog.IsExplored(hero.Owner, p))
            .OrderBy(p => Math.Max(Math.Abs(p.X - goal.X), Math.Abs(p.Y - goal.Y)))
            .ThenBy(p => p.Y).ThenBy(p => p.X)
            .Take(40)
            .FirstOrDefault(p => session.Movement.FindPath(hero.Id, p) is not null) is { } found && found != default
            ? found
            : null;
    }

    private static void Settle(GameSession session)
    {
        var state = session.State;
        while (state.PendingDecision is { } decision)
        {
            var player = state.CurrentPlayer.Id;
            // The timer's defaults (TurnTimer.Default), except where the Sim chooses otherwise: it takes what a witch hut or a training offers,
            // answers riddles and level-ups with the first option, leaves boxes shut and always fights a guard (it exercises battles).
            var command = decision switch
            {
                SphinxDecision => new AnswerSphinxCommand(player, 0),
                PandorasBoxDecision => new AnswerPandorasBoxCommand(player, Open: false),
                WitchHutDecision hut => new ChooseWitchHutSkillCommand(player, hut.Options[0]),
                TrainingDecision { CanDecline: false } training => new ChooseTrainingCommand(player, training.Options[0]),
                LevelUpDecision levelUp => new ChooseSkillCommand(player, levelUp.Options[0]),
                GuardEncounterDecision => new RespondToGuardCommand(player, Accept: false),
                _ => TurnTimer.Default(player, decision) ?? new AutoResolveBattleCommand(state.Battle!.Attacker.Owner ?? player),
            };
            session.Execute(command).ThrowIfRejected();
        }
    }
}
