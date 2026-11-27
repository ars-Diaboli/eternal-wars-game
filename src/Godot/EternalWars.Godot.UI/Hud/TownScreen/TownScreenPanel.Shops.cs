using System.Globalization;
using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Info;
using EternalWars.Core.Domain.Magic;
using EternalWars.Core.Domain.Towns;
using EternalWars.Godot.Shared.Controls;
using EternalWars.Godot.UI.Widgets;
using Godot;

namespace EternalWars.Godot.UI.Hud.TownScreen;

/// <summary>
/// The town's shops (ADR-296): the mage guild's shelves (or the war lodge's warcries) with the visiting hero's spells and the spell book; the
/// tavern's hero cards, rumour and Thieves' Guild; the marketplace's two columns of resources with the rate, the amount and Trade, the Silo, and
/// sending to a teammate; the blacksmith's machine cards.
/// </summary>
public partial class TownScreenPanel
{
    /// <summary>The spells the visiting hero learned in this town since the screen opened (the guild's "learned now" mark).</summary>
    private readonly HashSet<SpellId> _learnedHere = [];

    private ResourceType _give = ResourceType.Gold;
    private ResourceType _get = ResourceType.Ore;
    private int _amount = 1;

    // The mage guild --------------------------------------------------------------------------------------

    private void FillGuild(Town town, VBoxContainer body)
    {
        var state = State;
        var catalog = TownRules.Buildings(state, town);
        if (catalog.Contains(BuiltInBuildings.WarLodge) && !catalog.Contains(BuiltInBuildings.MageGuild))
        {
            FillWarLodge(town, body);
            return;
        }

        if (!catalog.Contains(BuiltInBuildings.MageGuild))
        {
            body.AddChild(Note($"{town.Name} has no mage guild."));
            return;
        }

        var hero = TownRules.VisitingHero(state, town);
        var guild = town.Level(BuiltInBuildings.MageGuild);
        var top = new HBoxContainer();
        top.AddChild(new Label { Text = hero is null ? "No hero is visiting: spells are learned on a visit." : $"{hero.Name} visits.", ThemeTypeVariation = UiTheme.QuietLabel });
        if (hero is not null && guild > 0)
        {
            top.AddChild(SpellBookButton(town, hero));
        }

        body.AddChild(top);
        for (var level = 1; level <= catalog.MaxLevel(BuiltInBuildings.MageGuild); level++)
        {
            var shelf = new PanelContainer { ThemeTypeVariation = UiTheme.TilePanel };
            var row = new HBoxContainer();
            shelf.AddChild(row);
            row.AddChild(new Label { Text = $"Level {level}", CustomMinimumSize = new Vector2(90, 0), VerticalAlignment = VerticalAlignment.Center });
            if (level > guild)
            {
                shelf.Modulate = new Color(0.55f, 0.55f, 0.6f);
                row.AddChild(Note($"A dark shelf: needs {catalog.Get(BuiltInBuildings.MageGuild, level).Name}."));
            }
            else
            {
                foreach (var spell in town.GuildSpells.GetValueOrDefault(level) ?? [])
                {
                    row.AddChild(SpellSlot(spell, hero, null));
                }

                foreach (var school in MagicRules.SchoolBuildings(town))
                {
                    var spells = MagicRules.SchoolSpells(state, town, school);
                    if (spells.Count >= level)
                    {
                        row.AddChild(SpellSlot(spells[level - 1], hero, UiFrames.Accent(school)));
                    }
                }
            }

            body.AddChild(shelf);
        }
    }

    /// <summary>A spell on a shelf: its icon and name, a mark when the hero knows it, "learned now" when it was learned on this visit, greyed with the reason when the hero cannot learn it.</summary>
    private Control SpellSlot(SpellId id, Hero? hero, Color? school)
    {
        var state = State;
        if (!state.Spells.TryGet(id, out var spell))
        {
            return new Label { Text = id.Value };
        }

        var slot = new PanelContainer { CustomMinimumSize = new Vector2(150, 0) };
        if (school is { } colour)
        {
            slot.AddThemeStyleboxOverride("panel", UiFrames.Themed(BevelKind.Slot, 4, tint: colour, flat: UiTheme.Box(UiTheme.ButtonFace, colour, 4, 2))); // the school's colour on the rim
        }

        var box = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        slot.AddChild(box);
        if (SpellIcons.Get(id, 48) is { } icon)
        {
            box.AddChild(new TextureRect { Texture = icon, StretchMode = TextureRect.StretchModeEnum.KeepCentered, MouseFilter = MouseFilterEnum.Ignore });
        }

        var known = hero is not null && MagicRules.Known(hero, state.Spells).Any(k => k.Spell.Id == id);
        var mark = _learnedHere.Contains(id) ? "✦ learned now" : known ? "✓ known" : "";
        box.AddChild(new Label { Text = spell.Name, HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore });
        if (mark.Length > 0)
        {
            var label = new Label { Text = mark, HorizontalAlignment = HorizontalAlignment.Center, ThemeTypeVariation = UiTheme.QuietLabel, MouseFilter = MouseFilterEnum.Ignore };
            label.AddThemeColorOverride("font_color", _learnedHere.Contains(id) ? UiTheme.TextCaution : UiTheme.TextComplete);
            box.AddChild(label);
        }
        else if (hero is not null && !MagicRules.CanLearn(hero, spell))
        {
            slot.Modulate = new Color(1, 1, 1, 0.5f);
            slot.TooltipText = hero.HasSpellBook
                ? $"{hero.Name} needs more {BuiltInSkills.SchoolGroup(spell.School)} magic for a level {spell.Level} spell."
                : $"{hero.Name} has no spell book.";
        }

        slot.WithInfo(detailed => InfoSheets.Spell(_session.State, id, 1, detailed, hero));
        return slot;
    }

    private Button SpellBookButton(Town town, Hero hero)
    {
        var refusal = MagicRules.CheckBuySpellBook(State, town);
        var book = new Button { Text = hero.HasSpellBook ? "Spell book (owned)" : "Buy a spell book", Disabled = refusal is not null, TooltipText = refusal ?? "" };
        if (!hero.HasSpellBook)
        {
            book.WithCost(MagicRules.SpellBookCost, treasury: Treasury);
        }
        else
        {
            book.WithIcon(UiIcon.SpellBook);
        }

        book.Pressed += () => Run(new BuySpellBookCommand(Player, town.Id));
        return book;
    }

    /// <summary>The war lodge (Stronghold, Swamp Bestiary, ADR-071): the warcries by level, the visiting hero's learned ones marked.</summary>
    private void FillWarLodge(Town town, VBoxContainer body)
    {
        var state = State;
        var catalog = TownRules.Buildings(state, town);
        var lodge = town.Level(BuiltInBuildings.WarLodge);
        var hero = TownRules.VisitingHero(state, town);
        body.AddChild(Note(hero is null ? "No hero is visiting: warcries are learned on a visit." : $"{hero.Name} has learned warcries up to level {hero.WarcriesLearned}."));
        foreach (var warcry in Enum.GetValues<Warcry>())
        {
            var level = PrimalFury.LevelOf(warcry);
            var row = new HBoxContainer();
            row.AddChild(new Label { Text = $"Level {level}", CustomMinimumSize = new Vector2(90, 0) });
            row.AddChild(new Label { Text = warcry.ToString(), CustomMinimumSize = new Vector2(160, 0) });
            if (level > lodge)
            {
                row.Modulate = new Color(0.55f, 0.55f, 0.6f);
                row.AddChild(Note(level <= catalog.MaxLevel(BuiltInBuildings.WarLodge) ? $"Needs {catalog.Get(BuiltInBuildings.WarLodge, level).Name}." : "Not taught here."));
            }
            else if (hero is not null && hero.WarcriesLearned >= level)
            {
                row.AddChild(Note("✓ learned", UiTheme.TextComplete));
            }

            body.AddChild(row);
        }
    }

    // The tavern ------------------------------------------------------------------------------------------

    private void FillTavern(Town town, VBoxContainer body)
    {
        var state = State;
        if (town.Level(BuiltInBuildings.Tavern) < 1)
        {
            body.AddChild(NotBuilt(town, BuiltInBuildings.Tavern));
            return;
        }

        var top = new HBoxContainer();
        top.AddChild(ResourceIcons.Balance(Treasury.Gold));
        var closed = ThievesGuild.Closed(state);
        var guild = new Button
        {
            Text = "Thieves' guild",
            Disabled = closed is not null,
            TooltipText = closed ?? "How you rank against the other players; more taverns show more",
        }.WithIcon(UiIcon.ThievesGuild);
        guild.Pressed += () => ThievesGuildRequested?.Invoke();
        top.AddChild(guild);
        body.AddChild(top);
        var cards = new HFlowContainer();
        var offers = TavernRules.Offers(state, Player, town);
        if (offers.Count == 0)
        {
            cards.AddChild(new Label { Text = "Nobody is looking for work this week." });
        }

        foreach (var offer in offers)
        {
            cards.AddChild(TavernCard.Hero(_session, town, offer, text => _status.Text = text));
        }

        body.AddChild(cards);
        if (TavernRules.Rumour(state, Player) is { } rumour)
        {
            body.AddChild(Note($"Rumour has it: {rumour}"));
        }
    }

    // The marketplace -------------------------------------------------------------------------------------

    private void FillMarketplace(Town town, VBoxContainer body)
    {
        var state = State;
        if (town.Level(BuiltInBuildings.Marketplace) < 1)
        {
            body.AddChild(NotBuilt(town, BuiltInBuildings.Marketplace));
            return;
        }

        var columns = new HBoxContainer();
        columns.AddThemeConstantOverride("separation", UiSpacing.Gap * 3);
        columns.AddChild(ResourceColumn("You give", true));
        var middle = new VBoxContainer { CustomMinimumSize = new Vector2(360, 0) };
        columns.AddChild(middle);
        columns.AddChild(ResourceColumn("You get", false));
        body.AddChild(columns);

        var trade = Trade(state);
        middle.AddChild(new Label { Text = trade.Rate, AutowrapMode = TextServer.AutowrapMode.WordSmart, HorizontalAlignment = HorizontalAlignment.Center });
        if (trade.Max > 0)
        {
            _amount = Math.Clamp(_amount, 1, trade.Max);
            var slider = new HSlider { MinValue = 1, MaxValue = trade.Max, Step = 1, Value = _amount, CustomMinimumSize = new Vector2(240, 0) };
            var amounts = new Label { HorizontalAlignment = HorizontalAlignment.Center };
            void Show() => amounts.Text = trade.Describe(_amount);
            slider.ValueChanged += v =>
            {
                _amount = (int)v;
                Show();
            };
            var max = new Button { Text = "Max" };
            max.Pressed += () => slider.Value = trade.Max;
            var row = new HBoxContainer();
            row.AddChild(slider);
            row.AddChild(max);
            middle.AddChild(row);
            middle.AddChild(amounts);
            Show();
            var go = new Button { Text = "Trade" };
            go.Pressed += () => Run(trade.Command(_amount));
            middle.AddChild(go);
        }
        else
        {
            middle.AddChild(Note(trade.Refusal ?? "", UiTheme.TextRefusal));
        }

        var level = town.Level(BuiltInBuildings.Marketplace);
        var income = TownRules.Buildings(state, town).Get(BuiltInBuildings.Marketplace, Math.Min(level, TownRules.Buildings(state, town).MaxLevel(BuiltInBuildings.Marketplace))).Income;
        if (income != ResourceSet.Empty)
        {
            var silo = new HBoxContainer();
            silo.AddChild(new Label { Text = BuildingName(town, BuiltInBuildings.Marketplace), ThemeTypeVariation = UiTheme.QuietLabel });
            silo.AddChild(ResourceIcons.CostRow(income, " a day"));
            body.AddChild(silo);
        }

        FillSendToAlly(body);
    }

    /// <summary>A column of the six resources; the picked one is pressed. Gold may be given or got, never both sides at once.</summary>
    private Control ResourceColumn(string title, bool give)
    {
        var column = new VBoxContainer();
        column.AddHeading(title);
        var group = new ButtonGroup();
        foreach (var resource in Enum.GetValues<ResourceType>())
        {
            var picked = give ? _give : _get;
            var button = new Button
            {
                Text = $"{resource} ({ResourceAmountText.Short(resource, Treasury[resource])})",
                ToggleMode = true,
                ButtonGroup = group,
                ButtonPressed = resource == picked,
                Alignment = HorizontalAlignment.Left,
                Disabled = resource == (give ? _get : _give),
            }.WithResourceIcon(resource);
            var chosen = resource;
            button.Pressed += () =>
            {
                if (give)
                {
                    _give = chosen;
                }
                else
                {
                    _get = chosen;
                }

                _amount = 1;
                if (Current is { } town)
                {
                    FillWindow(town);
                }
            };
            column.AddChild(button);
        }

        return column;
    }

    /// <summary>The trade the two columns make: buying with gold, selling for gold, or one resource for another (ADR-296).</summary>
    private (string Rate, int Max, string? Refusal, Func<int, string> Describe, Func<int, GameCommand> Command) Trade(Core.Domain.Game.GameState state)
    {
        var player = Player;
        var have = Treasury;
        string N(int n) => n.ToString(CultureInfo.InvariantCulture);
        if (_give == ResourceType.Gold)
        {
            var rate = MarketplaceRules.TownRate(state, player, _get)!.Value;
            var max = have.Gold / Math.Max(1, rate.BuyPrice);
            return ($"1 {_get} costs {N(rate.BuyPrice)} gold", max, $"Not enough gold for one {_get}.",
                n => $"{N(n)} {_get} for {N(n * rate.BuyPrice)} gold", n => new TradeResourceCommand(player, _get, TradeDirection.Buy, n));
        }

        if (_get == ResourceType.Gold)
        {
            var rate = MarketplaceRules.TownRate(state, player, _give)!.Value;
            return ($"1 {_give} sells for {N(rate.SellPrice)} gold", have[_give], $"You have no {_give} to sell.",
                n => $"{N(n)} {_give} for {N(n * rate.SellPrice)} gold", n => new TradeResourceCommand(player, _give, TradeDirection.Sell, n));
        }

        var giveRate = MarketplaceRules.TownRate(state, player, _give)!.Value;
        var getRate = MarketplaceRules.TownRate(state, player, _get)!.Value;
        var most = MarketplaceRules.MaxExchange(giveRate, getRate, have[_give]);
        return ($"1 {_get} costs {N((int)MarketplaceRules.ExchangeCost(giveRate, getRate, 1))} {_give}", most, $"Not enough {_give} for one {_get}.",
            n => $"{N((int)MarketplaceRules.ExchangeCost(giveRate, getRate, n))} {_give} for {N(n)} {_get}", n => new ExchangeResourcesCommand(player, _give, _get, n));
    }

    /// <summary>Sending resources to a teammate (ADR-296): shown only when the player has one still in the game, never in a tournament (ADR-366).</summary>
    private void FillSendToAlly(VBoxContainer body)
    {
        var state = State;
        var allies = state.Players.Where(p => !p.IsDefeated && state.AreAllies(Player, p.Id)).ToList();
        if (allies.Count == 0 || state.Rules.Tournament) // a tournament has no trading (ADR-366)
        {
            return;
        }

        body.AddChild(new HSeparator());
        body.AddHeading("Send to an ally");
        var to = new OptionButton();
        foreach (var ally in allies)
        {
            to.AddItem(ally.Name);
        }

        var row = new HBoxContainer();
        row.AddChild(to);
        var amounts = new Dictionary<ResourceType, SpinBox>();
        foreach (var resource in Enum.GetValues<ResourceType>())
        {
            row.AddChild(ResourceIcons.Badge(resource));
            var spin = new SpinBox { MinValue = 0, MaxValue = Math.Max(0, Treasury[resource]), Step = 1, CustomMinimumSize = new Vector2(90, 0), TooltipText = resource.ToString() };
            amounts[resource] = spin;
            row.AddChild(spin);
        }

        var send = new Button { Text = "Send" };
        send.Pressed += () =>
        {
            var amount = amounts.Aggregate(ResourceSet.Empty, (total, a) => total + ResourceSet.Of(a.Key, (int)a.Value.Value));
            Run(new SendResourcesCommand(Player, allies[Math.Max(0, to.Selected)].Id, amount));
        };
        row.AddChild(send);
        body.AddChild(row);
    }

    // The blacksmith --------------------------------------------------------------------------------------

    private void FillBlacksmith(Town town, VBoxContainer body)
    {
        var state = State;
        if (town.Level(BuiltInBuildings.Blacksmith) < 1)
        {
            body.AddChild(NotBuilt(town, BuiltInBuildings.Blacksmith));
            return;
        }

        var hero = TownRules.VisitingHero(state, town);
        body.AddChild(Note(hero is null ? "No hero is visiting: the machines are sold to a hero at the entrance." : $"Machines for {hero.Name}."));
        var cards = new HBoxContainer();
        cards.AddThemeConstantOverride("separation", UiSpacing.Gap * 2);
        foreach (var slot in Enum.GetValues<WarMachineSlot>())
        {
            var kind = WarMachines.KindOf(slot, hero is null ? town.TownType : ProgressionService.TownOf(state, hero));
            var machine = WarMachines.Definition(kind);
            var card = new PanelContainer { ThemeTypeVariation = UiTheme.TilePanel, CustomMinimumSize = new Vector2(240, 0) };
            var box = new VBoxContainer();
            card.AddChild(box);
            if (HeroIcons.Machine(kind, 96) is { } icon)
            {
                box.AddChild(new TextureRect { Texture = icon, StretchMode = TextureRect.StretchModeEnum.KeepCentered });
            }

            box.AddChild(new Label { Text = machine.Name, ThemeTypeVariation = UiTheme.HeadingLabel });
            box.AddChild(Note($"Attack {machine.Attack}, Defense {machine.Defense}, damage {machine.MinDamage}–{machine.MaxDamage}, health {machine.Health}"));
            var owned = hero?.WarMachines.Contains(slot) == true;
            if (owned)
            {
                box.AddChild(Note($"{hero!.Name} has one.", UiTheme.TextComplete));
            }

            var refusal = TownRules.CheckBuyWarMachine(state, town, slot);
            var buy = new Button { Text = $"Buy {machine.Name}", Disabled = refusal is not null, TooltipText = refusal ?? "" }
                .WithCost(WarMachines.Cost(slot), treasury: Treasury);
            buy.Pressed += () => Run(new BuyWarMachineCommand(Player, town.Id, slot));
            box.AddChild(buy);
            if (refusal is not null && !owned)
            {
                box.AddChild(Note(refusal, UiTheme.TextRefusal));
            }

            card.WithInfo(detailed => InfoSheets.Creature(_session.State, machine.Id, detailed));
            cards.AddChild(card);
        }

        body.AddChild(cards);
    }
}
