using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Magic;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Events;
using EternalWars.Godot.Shared.Controls;
using EternalWars.Godot.Shared.Events;
using Godot;

namespace EternalWars.Godot.UI.Hud.TownScreen;

/// <summary>
/// What the town sells the visiting hero: the blacksmith's war machines it lacks (ADR-056) and the mage guild's spell book (ADR-071, ADR-110);
/// then what the guild teaches now.
/// </summary>
internal sealed class BlacksmithSection(TownPanel panel) : TownSection(panel)
{
    public HBoxContainer Row { get; } = new();

    protected override Node Anchor => Row;

    public override void Show(Town town)
    {
        var state = State;
        Row.ClearChildren();
        if (TownRules.VisitingHero(state, town) is { } hero)
        {
            Row.AddChild(new Label { Text = $"Blacksmith (war machines of {hero.Name})", CustomMinimumSize = new Vector2(UiTheme.TownLineWidth, 0) });
            foreach (var slot in Enum.GetValues<WarMachineSlot>())
            {
                var name = TownRules.WarMachineName(state, hero, slot);
                var refusal = TownRules.CheckBuyWarMachine(state, town, slot);
                var owned = hero.WarMachines.Contains(slot);
                var buy = new Button
                {
                    Text = owned ? $"{name} (owned)" : $"Buy {name}",
                    Disabled = refusal is not null,
                    TooltipText = refusal ?? "",
                }.WithMachineIcon(state, hero, slot);
                if (!owned)
                {
                    buy.WithCost(WarMachines.Cost(slot));
                }

                buy.Pressed += () => Run(new BuyWarMachineCommand(state.CurrentPlayer.Id, town.Id, slot));
                Row.AddChild(buy);
            }

            if (town.Level(BuiltInBuildings.MageGuild) > 0)
            {
                var refusal = MagicRules.CheckBuySpellBook(state, town);
                var book = new Button
                {
                    Text = hero.HasSpellBook ? "Spell book (owned)" : "Buy a spell book",
                    Disabled = refusal is not null,
                    TooltipText = refusal ?? "",
                };
                if (!hero.HasSpellBook)
                {
                    book.WithCost(MagicRules.SpellBookCost);
                }
                else
                {
                    book.WithIcon(UiIcon.SpellBook); // a priced button lays its own lines out; the icon goes on the owned one only
                }

                book.Pressed += () => Run(new BuySpellBookCommand(state.CurrentPlayer.Id, town.Id));
                Row.AddChild(book);
            }
        }

        // What the guild teaches now (ADR-110).
        var offered = MagicRules.Offered(state, town).Select(s => state.Spells.TryGet(s, out var spell) ? spell.Name : s).ToList();
        if (offered.Count > 0)
        {
            Row.AddChild(new Label { Text = $"Mage guild: {string.Join(", ", offered)}" });
        }
    }

    /// <summary>The visiting hero, what it bought, and what the player can pay for.</summary>
    protected override void Subscribe(RefreshScheduler refresh, IEventSubscriber events) =>
        OnHeroPresence(refresh, events)
            .On<WarMachineBought>(events)
            .On<SpellBookBought>(events)
            .On<ResourcesChanged>(events);
}
