using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Info;
using EternalWars.Core.Domain.Magic;
using EternalWars.Core.Domain.Towns;
using EternalWars.Godot.Shared.Controls;
using Godot;

namespace EternalWars.Godot.UI.Hud;

/// <summary>
/// The buttons for a hero's adventure spells (ADR-120), shared by the hero screen and the spell book (ADR-210): one per spell, disabled with
/// the reason while it cannot be cast, and the week-long ranks III read again (the Ledger of Ages, Eavesdrop).
/// </summary>
internal static class AdventureSpellButtons
{
    /// <param name="cast">A spell cast at once (no cell to pick).</param>
    /// <param name="arm">A spell aimed at a cell: the map's next click is its cell.</param>
    /// <param name="ledger">Read the Ledger of Ages again.</param>
    /// <param name="eavesdrop">Listen at the town again.</param>
    public static List<Button> For(
        GameState state, Hero hero, Action<string> cast, Action<string> arm, Action ledger, Action<MapObjectId> eavesdrop)
    {
        var buttons = new List<Button>();
        foreach (var spell in AdventureMagic.Known(state, hero))
        {
            var refusal = AdventureMagic.Refusal(state, hero, spell.Id, null, aimed: false); // an aimed spell's cell is picked on the map
            var rank = AdventureMagic.RankOf(state, hero, spell.Id);
            var button = new Button
            {
                Text = $"{MagicRules.NameAtRank(spell, rank)} ({AdventureMagic.Cost(state, hero, spell.Id)} mana)",
                Disabled = refusal is not null,
                TooltipText = refusal ?? spell.Notes,
            };
            var id = spell.Id;
            button.WithInfo(detailed => InfoSheets.Spell(state, id, rank, detailed, hero));
            button.Pressed += () =>
            {
                if (AdventureMagic.NeedsTarget(state, hero, id))
                {
                    arm(id.Value);
                }
                else
                {
                    cast(id.Value);
                }
            };
            buttons.Add(button);
        }

        if (AdventureMagic.ReadsLedger(hero))
        {
            var again = new Button { Text = "Read the Ledger of Ages again" };
            again.Pressed += ledger;
            buttons.Add(again);
        }

        if (AdventureMagic.ListensTo(hero) is { } town && state.FindObject(town) is Town listened)
        {
            var again = new Button { Text = $"Listen at {listened.Name} again" };
            again.Pressed += () => eavesdrop(town);
            buttons.Add(again);
        }

        return buttons;
    }
}
