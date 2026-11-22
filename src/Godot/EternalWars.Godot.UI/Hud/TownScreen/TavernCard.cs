using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Info;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;
using EternalWars.Godot.Shared.Controls;
using EternalWars.Godot.Shared.Events;
using Godot;

namespace EternalWars.Godot.UI.Hud.TownScreen;

/// <summary>The tavern window's hero cards (ADR-060, ADR-081, ADR-296).</summary>
internal static class TavernCard
{
    /// <summary>
    /// A hero the tavern offers as a card: crest and name, class and faction, specialty, skills,
    /// primary stats, the preferred creature, the troops it brings or, for a returning hero, its level and army (the "returns" badge), and Hire.
    /// <paramref name="status"/> gets what the hire did.
    /// </summary>
    internal static Control Hero(GameSession session, Town town, HeroDefinition offer, Action<string> status)
    {
        var state = session.State;
        var player = state.CurrentPlayer.Id;
        var card = new PanelContainer { CustomMinimumSize = new Vector2(250, 0), ThemeTypeVariation = UiTheme.TilePanel }
            .WithInfo(detailed => InfoSheets.TavernHero(session.State, player, town, offer, detailed));
        var box = new VBoxContainer();
        card.AddChild(box);
        Label Line(string text, Color? color = null)
        {
            var label = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(240, 0), Modulate = color ?? Colors.White };
            box.AddChild(label);
            return label;
        }

        var returning = TavernRules.Returning(state, player, offer.Id);
        box.AddChild(Heraldry.Titled(offer.Town, offer.Name, Heraldry.TitleSize, new Color(1f, 0.9f, 0.6f)));
        if (returning is not null)
        {
            Line("◆ Returns with what it kept", new Color(0.7f, 0.9f, 1f));
        }

        Line($"{offer.Class}, {offer.Town}{(offer.Town == town.TownType ? " (native)" : "")}");
        Line($"Specialty: {offer.Specialty.Describe(state.Spells)}");
        var skills = returning?.Skills.ToString() ?? string.Join(", ", offer.StartingSkills.Select(s => $"{s.Value} {(BuiltInSkills.Contains(s.Key) ? BuiltInSkills.Get(s.Key).Name : s.Key)}"));
        Line($"Skills: {skills}");
        Line(returning is { } back
            ? $"Attack {back.Attack}, Defense {back.Defense}, Power {back.SpellPower}, Knowledge {back.Knowledge}"
            : $"Attack {offer.Attack}, Defense {offer.Defense}, Power {offer.SpellPower}, Knowledge {offer.Knowledge}");
        var preferred = state.Creatures.TryGet(offer.PreferredCreature, out var creature) ? creature.Name : offer.PreferredCreature;
        var prefers = Line($"Prefers {preferred} (+{SkillEffects.PreferredAttack} Attack, +{SkillEffects.PreferredDefense} Defense, +{SkillEffects.PreferredSpeed} speed)");
        if (creature is not null)
        {
            prefers.WithInfo(detailed => InfoSheets.Creature(session.State, creature.Id, detailed));
        }

        if (returning is { } hero)
        {
            Line($"Returns at level {hero.Level}{(hero.Army.IsEmpty ? ", without an army" : $" with {string.Join(", ", hero.Army.Stacks.Select(s => $"{s.Stack.Count} {state.Creatures.Get(s.Stack.Creature).Name}"))}")}", new Color(0.7f, 0.9f, 1f));
        }
        else if (TavernRules.StarterStack(state, offer) is { } stack)
        {
            Line($"Brings {stack.Count} {state.Creatures.Get(stack.Creature).Name}")
                .WithInfo(detailed => InfoSheets.Creature(session.State, stack.Creature, detailed, InfoSheets.Known(stack)));
        }

        var refusal = TavernRules.CheckHire(state, town, offer.Id);
        var hire = new Button { Text = "Hire", Disabled = refusal is not null, TooltipText = refusal ?? $"{offer.Name} appears at the town's entrance." }
            .WithCost(TavernRules.HeroCost, treasury: state.CurrentPlayer.Resources)
            .WithInfo(detailed => InfoSheets.TavernHero(session.State, player, town, offer, detailed));
        hire.Pressed += () =>
        {
            var result = session.Execute(new HireHeroCommand(session.State.CurrentPlayer.Id, town.Id, offer.Id));
            status(result.IsAccepted ? $"{offer.Name} joins you." : $"Refused: {result.Error}");
        };
        box.AddChild(hire);
        if (refusal is not null)
        {
            Line(refusal, UiTheme.TextRefusal);
        }

        return card;
    }
}
