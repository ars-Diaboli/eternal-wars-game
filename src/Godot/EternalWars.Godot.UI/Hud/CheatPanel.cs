using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Game;
using Godot;

namespace EternalWars.Godot.UI.Hud;

/// <summary>
/// The play-test's cheat panel (EDIT-5, ADR-267): it exists only in a game started with <c>--cheats</c> and changes the game only through the
/// cheat commands (ADR-036), for the player whose turn it is and the selected hero: resources, levels, more of the hero's first creature.
/// What the game refuses is told through the status line.
/// </summary>
public partial class CheatPanel : PanelContainer
{
    /// <summary>What "Resources" gives.</summary>
    public static readonly ResourceSet Resources = new(Gold: 10000, Lumber: 20, Ore: 20, Crystal: 10, Mercury: 10, Sulfur: 10);

    private GameSession _session = null!;
    private Func<HeroId?> _hero = null!;
    private Action<string> _tell = null!;

    /// <summary><paramref name="hero"/> is the selected hero (null: none); <paramref name="tell"/> shows a line in the status bar.</summary>
    public void Bind(GameSession session, Func<HeroId?> hero, Action<string> tell)
    {
        _session = session;
        _hero = hero;
        _tell = tell;
        Name = "Cheats";
        var rows = new VBoxContainer();
        AddChild(rows);
        rows.AddChild(new Label { Text = "Cheats (play-test)", ThemeTypeVariation = "HeaderSmall" });
        Add(rows, "CheatResources", "Resources", "10 000 gold, 20 lumber and ore, 10 of each other resource", () =>
            new CheatResourcesCommand(Player, Resources));
        Add(rows, "CheatLevel", "+1 level", "The selected hero's experience for one more level", () =>
            _hero() is { } hero ? new CheatLevelsCommand(Player, hero, 1) : null);
        Add(rows, "CheatCreatures", "+10 creatures", "Ten more of the selected hero's first creature", () =>
            _hero() is { } hero && _session.State.GetHero(hero).Army.Stacks.FirstOrDefault() is { } first
                ? new CheatCreaturesCommand(Player, hero, first.Stack.Creature, 10)
                : null);
    }

    private PlayerId Player => _session.State.CurrentPlayer.Id;

    private void Add(VBoxContainer rows, string name, string text, string tip, Func<GameCommand?> command)
    {
        var button = new Button { Name = name, Text = text, TooltipText = tip, FocusMode = FocusModeEnum.None };
        button.Pressed += () =>
        {
            if (command() is not { } chosen)
            {
                _tell("Select a hero with an army first.");
                return;
            }

            var result = _session.Execute(chosen);
            _tell(result.IsAccepted ? $"Cheat: {text}." : $"Cheat refused: {result.Error}");
        };
        rows.AddChild(button);
    }
}
