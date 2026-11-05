using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Artifacts;
using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Magic;
using EternalWars.Core.Domain.MapEvents;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;
using EternalWars.Godot.Shared.Events;
using Godot;

namespace EternalWars.Godot.UI.Editor;

/// <summary>
/// Edits the selected hero (stats, Tactics, seven army slots) or object (mine owner and guard, chest values,
/// monster creature/count/disposition/guard mode, town owner, name, buildings, dwellings, guild spells and garrison, a Pandora's Box's contents, guard and
/// message; gate links are shown; links to the events a box runs, a quest object asks and a town has).
/// Every change is an undoable editor edit.
/// </summary>
public partial class InspectorPanel : VBoxContainer
{
    private MapEditorSession _editor = null!;
    private HeroId? _hero;
    private MapObjectId? _object;
    private bool _refreshing;

    /// <summary>"Edit" on an event link: the editor opens that event in the "Events" tab.</summary>
    public event Action<string>? EventRequested;

    public void Bind(MapEditorSession editor)
    {
        _editor = editor;
        Refresh();
        this.SubscribeWhileInTree<UndoHistoryChanged>(editor.Events, _ => Refresh());
    }

    public void InspectHero(HeroId hero)
    {
        (_hero, _object) = (hero, null);
        Refresh();
    }

    public void InspectObject(MapObjectId mapObject)
    {
        (_hero, _object) = (null, mapObject);
        Refresh();
    }

    private void Refresh()
    {
        _refreshing = true;
        foreach (var child in GetChildren())
        {
            child.QueueFree();
        }

        var scenario = _editor.Scenario;
        if (_hero is { } heroId && scenario.Heroes.FirstOrDefault(h => h.Id == heroId) is { } hero)
        {
            ShowHero(hero);
        }
        else if (_object is { } objectId && scenario.FindObject(objectId) is { } mapObject)
        {
            ShowObject(mapObject);
        }
        else
        {
            AddChild(new Label { Text = "Use the Select tool and click a hero or object." });
        }

        _refreshing = false;
    }

    private void ShowHero(Hero hero)
    {
        AddChild(new Label { Text = $"{hero.Name} ({_editor.Scenario.GetPlayer(hero.Owner).Name}) at {hero.Position}" });
        var identity = new HBoxContainer();
        AddChild(identity);
        var type = Options(identity, "Hero", new[] { ("(custom)", (string?)null) }
            .Concat(_editor.Scenario.HeroRoster.All.OrderBy(h => h.Town).ThenBy(h => h.Name)
                .Select(h => ($"{h.Name} ({h.Town}, {h.Class})", (string?)h.Id))), hero.Definition);
        type.Button.ItemSelected += _ => Edit(() => _editor.SetHeroType(hero.Id, type.Value()));
        var placeholder = new CheckBox
        {
            Text = "Placeholder",
            ButtonPressed = hero.IsPlaceholder,
            TooltipText = "Becomes a random roster hero of its owner's faction when the game starts (ADR-126); its place, army and artifacts stay.",
        };
        placeholder.Toggled += on => Edit(() => _editor.SetHeroPlaceholder(hero.Id, on));
        identity.AddChild(placeholder);
        if (hero.IsPlaceholder)
        {
            AddChild(new Label { Text = "Placeholder: a random hero of the owner's faction replaces its type, stats, skills and spells at game start." });
        }

        if (hero.Definition is not null)
        {
            AddChild(new Label { Text = $"{hero.Class}; prefers {hero.PreferredCreature}; specialty {hero.Specialty}; skills {hero.Skills}" });
        }

        var stats = new HBoxContainer();
        AddChild(stats);
        var attack = Spin(stats, "Attack", hero.Attack, 0, 99);
        var defense = Spin(stats, "Defense", hero.Defense, 0, 99);
        var spellPower = Spin(stats, "Power", hero.SpellPower, 0, 99);
        var knowledge = Spin(stats, "Knowledge", hero.Knowledge, 0, 99);
        var tactics = Options(stats, "Tactics", Enum.GetValues<SkillLevel>().Where(s => s <= SkillLevel.Expert).Select(s => (s.ToString(), s)), hero.Tactics);
        void ApplyStats() => Edit(() => _editor.SetHeroStats(
            hero.Id, (int)attack.Value, (int)defense.Value, tactics.Value(), (int)spellPower.Value, (int)knowledge.Value));
        attack.ValueChanged += _ => ApplyStats();
        defense.ValueChanged += _ => ApplyStats();
        spellPower.ValueChanged += _ => ApplyStats();
        knowledge.ValueChanged += _ => ApplyStats();
        tactics.Button.ItemSelected += _ => ApplyStats();

        var spirit = new HBoxContainer();
        AddChild(spirit);
        var morale = Spin(spirit, "Morale", hero.Morale, -5, 5);
        var luck = Spin(spirit, "Luck", hero.Luck, -5, 5);
        void ApplySpirit() => Edit(() => _editor.SetHeroMoraleAndLuck(hero.Id, (int)morale.Value, (int)luck.Value));
        morale.ValueChanged += _ => ApplySpirit();
        luck.ValueChanged += _ => ApplySpirit();

        var machines = new HBoxContainer();
        AddChild(machines);
        machines.AddChild(new Label { Text = "War machines" });
        var boxes = Enum.GetValues<WarMachineSlot>().Select(slot =>
        {
            var box = new CheckBox { Text = slot.ToString(), ButtonPressed = hero.WarMachines.Contains(slot) };
            machines.AddChild(box);
            return (Slot: slot, Box: box);
        }).ToList();
        foreach (var (_, box) in boxes)
        {
            box.Toggled += _ => Edit(() => _editor.SetHeroWarMachines(hero.Id, boxes.Where(b => b.Box.ButtonPressed).Select(b => b.Slot).ToHashSet()));
        }

        AddChild(new Label { Text = "Army" });
        var creatures = new[] { ("— empty —", "") }
            .Concat(_editor.Scenario.Creatures.All.OrderBy(c => c.Town ?? "~").ThenBy(c => c.Tier).Select(c => ($"{c.Name} (T{c.Tier})", c.Id)))
            .ToList();
        for (var slot = 0; slot < Army.SlotCount; slot++)
        {
            var row = new HBoxContainer();
            AddChild(row);
            var stack = hero.Army[slot];
            var creature = Options(row, $"{slot + 1}", creatures, stack?.Creature ?? "");
            var count = Spin(row, "×", stack?.Count ?? 1, 1, 9999);
            var index = slot;
            void ApplySlot() => Edit(() => _editor.SetArmySlot(
                hero.Id, index, creature.Value() is { Length: > 0 } id ? new CreatureStack(id, (int)count.Value) : null));
            creature.Button.ItemSelected += _ => ApplySlot();
            count.ValueChanged += _ => ApplySlot();
        }

        ShowHeroSpells(hero);
        ShowHeroArtifacts(hero);
    }

    /// <summary>The hero's spell book and the spells in it (ADR-120): a book box, one row per spell and an empty row to add another.</summary>
    private void ShowHeroSpells(Hero hero)
    {
        var book = new CheckBox { Text = "Spell book", ButtonPressed = hero.HasSpellBook };
        AddChild(book);
        var items = new[] { ("— none —", "") }
            .Concat(_editor.Scenario.Spells.All.OrderBy(s => s.Level).ThenBy(s => s.Name).Select(s => ($"{s.Name} (L{s.Level}, {s.School})", s.Id)))
            .ToList();
        var rows = new HFlowContainer();
        AddChild(rows);
        var choices = hero.Spells.Keys.Append("").Select(id => Options(rows, "", items, id)).ToList();
        rows.Visible = hero.HasSpellBook;
        void Apply() => Edit(() => _editor.SetHeroSpells(
            hero.Id, book.ButtonPressed, book.ButtonPressed ? [.. choices.Select(c => c.Value()).Where(s => s.Length > 0).Distinct()] : []));
        book.Toggled += _ => Apply();
        foreach (var choice in choices)
        {
            choice.Button.ItemSelected += _ => Apply();
        }
    }

    /// <summary>
    /// What the hero wears and carries (ADR-061): one row per worn artifact (it goes to the slot its definition names) and per backpack item, and one
    /// empty row each to add another; "none" removes an entry. A set that does not fit the slots is refused (the warning names it).
    /// </summary>
    private void ShowHeroArtifacts(Hero hero)
    {
        var scenario = _editor.Scenario;
        var items = new[] { ("— none —", "") }
            .Concat(scenario.Artifacts.All.OrderBy(a => a.Slot).ThenBy(a => a.Tier).ThenBy(a => a.Name).Select(a => ($"{a.Name} ({a.Slot}, {a.Tier})", a.Id)))
            .ToList();

        // Written scrolls the hero has (ADR-136) are not catalog rows; list them so an edit keeps them. A blank scroll gets its spell at game start.
        foreach (var id in hero.Artifacts.AllWorn.Select(w => w.Id).Concat(hero.Artifacts.Backpack).Where(id => items.All(i => i.Item2 != id)).Distinct())
        {
            items.Add((scenario.Artifacts.TryGet(id, out var written) ? $"{written.Name} ({written.Slot}, {written.Tier})" : id, id));
        }
        var changed = new List<Choice<string>>();
        List<Choice<string>> Rows(string title, IEnumerable<string> ids)
        {
            AddChild(new Label { Text = title });
            var rows = new HFlowContainer();
            AddChild(rows);
            return ids.Append("").Select(id =>
            {
                var choice = Options(rows, "", items, id);
                changed.Add(choice);
                return choice;
            }).ToList();
        }

        var worn = Rows("Worn artifacts", hero.Artifacts.AllWorn.Select(w => w.Id));
        var backpack = Rows("Backpack", hero.Artifacts.Backpack);
        void Apply() => Edit(() => _editor.SetHeroArtifacts(
            hero.Id,
            [.. worn.Select(w => w.Value()).Where(a => a.Length > 0)],
            [.. backpack.Select(b => b.Value()).Where(a => a.Length > 0)]));
        foreach (var choice in changed)
        {
            choice.Button.ItemSelected += _ => Apply();
        }
    }

    private void ShowObject(MapObject mapObject)
    {
        AddChild(new Label { Text = $"{mapObject.Definition.Name} #{mapObject.Id.Value} at {mapObject.Position}" });
        var creatures = _editor.Scenario.Creatures.All.OrderBy(c => c.Town ?? "~").ThenBy(c => c.Tier).Select(c => ($"{c.Name} (T{c.Tier})", c.Id)).ToList();
        switch (mapObject)
        {
            case CreatureDwelling dwelling:
            {
                var row = new HBoxContainer();
                AddChild(row);
                var lines = creatures.Where(c => !_editor.Scenario.Creatures.IsUpgrade(c.Id)).ToList(); // base creatures only (ADR-073)
                var creature = Options(row, "Creature", lines, dwelling.Creature);
                var owners = new[] { ("Unowned", (PlayerId?)null) }.Concat(_editor.Scenario.Players.Select(p => (p.Name, (PlayerId?)p.Id)));
                var owner = Options(row, "Owner", owners, dwelling.Owner);
                var amountRow = new HBoxContainer();
                AddChild(amountRow);
                var amount = Spin(amountRow, "Waiting (-1 = one week's growth)", dwelling.Available ?? -1, -1, 9999);
                void Apply() => Edit(() => _editor.SetDwelling(dwelling.Id, creature.Value(), owner.Value(), amount.Value >= 0 ? (int)amount.Value : null));
                creature.Button.ItemSelected += _ => Apply();
                owner.Button.ItemSelected += _ => Apply();
                amount.ValueChanged += _ => Apply();
                break;
            }

            case ResourceProvider provider:
            {
                var row = new HBoxContainer();
                AddChild(row);
                var owners = new[] { ("Unowned", (PlayerId?)null) }.Concat(_editor.Scenario.Players.Select(p => (p.Name, (PlayerId?)p.Id)));
                var owner = Options(row, "Owner", owners, provider.Owner);
                owner.Button.ItemSelected += _ => Edit(() => _editor.SetProviderOwner(provider.Id, owner.Value()));
                break;
            }

            case Mine mine:
            {
                var row = new HBoxContainer();
                AddChild(row);
                var owners = new[] { ("Unowned", (PlayerId?)null) }.Concat(_editor.Scenario.Players.Select(p => (p.Name, (PlayerId?)p.Id)));
                var owner = Options(row, "Owner", owners, mine.Owner);
                owner.Button.ItemSelected += _ => Edit(() => _editor.SetMineOwner(mine.Id, owner.Value()));

                var guardRow = new HBoxContainer();
                AddChild(guardRow);
                var guardCreature = Options(guardRow, "Guard", new[] { ("None", "") }.Concat(creatures), mine.Guard?.Creature ?? "");
                var guardCount = Spin(guardRow, "Count (0 = rolled)", mine.Guard?.Count ?? 0, 0, 999);
                void ApplyGuard() => Edit(() => _editor.SetMineGuard(mine.Id, guardCreature.Value() is { Length: > 0 } id
                    ? new GuardStack(id, guardCount.Value > 0 ? (int)guardCount.Value : null)
                    : null));
                guardCreature.Button.ItemSelected += _ => ApplyGuard();
                guardCount.ValueChanged += _ => ApplyGuard();
                break;
            }

            case MapGarrison garrison:
            {
                var row = new HBoxContainer();
                AddChild(row);
                var owners = new[] { ("Neutral", (PlayerId?)null) }.Concat(_editor.Scenario.Players.Select(p => (p.Name, (PlayerId?)p.Id)));
                var owner = Options(row, "Owner", owners, garrison.Owner);
                owner.Button.ItemSelected += _ => Edit(() => _editor.SetMapGarrisonOwner(garrison.Id, owner.Value()));
                GarrisonRows(garrison.Id, garrison.Troops);
                break;
            }

            case SignPost sign:
            {
                var row = new HBoxContainer();
                AddChild(row);
                row.AddChild(new Label { Text = "Message" });
                var text = new LineEdit { Text = sign.Message, CustomMinimumSize = new Vector2(260, 0), PlaceholderText = "What the sign says" };
                text.TextSubmitted += value => Edit(() => _editor.SetSignMessage(sign.Id, value));
                text.FocusExited += () => Edit(() => _editor.SetSignMessage(sign.Id, text.Text));
                row.AddChild(text);
                break;
            }

            case Prison prison:
            {
                var row = new HBoxContainer();
                AddChild(row);
                var heroes = new[] { ("Random (drawn at start)", (string?)null) }
                    .Concat(_editor.Scenario.HeroRoster.All.OrderBy(h => h.Town).ThenBy(h => h.Name).Select(h => ($"{h.Name} ({h.Town})", (string?)h.Id)));
                var who = Options(row, "Prisoner", heroes, prison.Hero);
                var level = Spin(row, "Level (0 = rolled)", prison.Level ?? 0, 0, 75);
                void ApplyPrison() => Edit(() => _editor.SetPrison(prison.Id, who.Value(), level.Value == 0 ? null : (int)level.Value));
                who.Button.ItemSelected += _ => ApplyPrison();
                level.ValueChanged += _ => ApplyPrison();
                break;
            }

            case ArtifactPickup pickup:
            {
                var row = new HBoxContainer();
                AddChild(row);
                var artifacts = new[] { ($"Random ({pickup.Definition.Name}, drawn at start)", (string?)null) }
                    .Concat(_editor.Scenario.Artifacts.All.OrderBy(a => a.Tier).ThenBy(a => a.Name).Select(a => ($"{a.Name} ({a.Tier})", (string?)a.Id)));
                var catalog = _editor.Scenario.Artifacts;
                var artifact = Options(row, "Artifact", artifacts, pickup.Artifact is { } current ? ArtifactCatalog.BaseOf(current) : null);
                artifact.Button.ItemSelected += _ => Edit(() => _editor.SetArtifactPickup(pickup.Id, artifact.Value()));

                // A spell scroll (ADR-136): the spell written on it, or a random one of its level drawn at game start.
                if (pickup.Artifact is { } chosen && catalog.TryGet(chosen, out var scroll) && scroll.ScrollLevel is { } level)
                {
                    var spellRow = new HBoxContainer();
                    AddChild(spellRow);
                    var spells = new[] { ($"Random level {level} spell (drawn at start)", (string?)null) }
                        .Concat(catalog.ScrollSpells(level).Select(id => catalog.Spells.Get(id)).OrderBy(s => s.Name).Select(s => ($"{s.Name} ({s.School})", (string?)s.Id)));
                    var spell = Options(spellRow, "Spell", spells, scroll.Spell);
                    spell.Button.ItemSelected += _ => Edit(() => _editor.SetArtifactPickup(pickup.Id,
                        spell.Value() is { } written ? ArtifactCatalog.WithSpell(chosen, written) : ArtifactCatalog.BaseOf(chosen)));
                }

                break;
            }

            case Sphinx sphinx:
                ShowSphinx(sphinx);
                break;

            case WitchHut hut:
            {
                var row = new HBoxContainer();
                AddChild(row);
                var skills = new[] { ("Rolled at start", "") }.Concat(BuiltInSkills.All.OrderBy(s => s.Group).ThenBy(s => s.Name).Select(s => ($"{s.Name} ({s.Group})", s.Id))).ToList();
                var first = Options(row, "Skills", skills, hut.Skills.ElementAtOrDefault(0) ?? "");
                var second = Options(row, "and", skills, hut.Skills.ElementAtOrDefault(1) ?? "");
                AddChild(new Label { Text = "Set both, or leave both rolled; the two must differ." });
                void ApplyHut()
                {
                    // Both or neither: a half-set hut waits for its second skill.
                    if ((first.Value().Length > 0) == (second.Value().Length > 0))
                    {
                        Edit(() => _editor.SetWitchHutSkills(hut.Id, first.Value().Length > 0 ? [first.Value(), second.Value()] : []));
                    }
                }

                first.Button.ItemSelected += _ => ApplyHut();
                second.Button.ItemSelected += _ => ApplyHut();
                break;
            }

            case Shrine shrine:
            {
                var row = new HBoxContainer();
                AddChild(row);
                var spells = new[] { ("Drawn at start", (string?)null) }
                    .Concat(ShrineRules.Candidates(_editor.Scenario.Spells, shrine.SpellLevel).Select(s => ($"{s.Name} ({s.School})", (string?)s.Id)));
                var spell = Options(row, "Spell", spells, shrine.Spell);
                spell.Button.ItemSelected += _ => Edit(() => _editor.SetShrineSpell(shrine.Id, spell.Value()));
                break;
            }

            case TreasureChest chest:            {
                var row = new HBoxContainer();
                AddChild(row);
                var fixedValues = new CheckBox { Text = "Fixed values (else rolled)", ButtonPressed = chest.FixedValues is not null };
                row.AddChild(fixedValues);
                var gold = Spin(row, "Gold", chest.FixedValues?.Gold ?? 1000, 0, 100000);
                var experience = Spin(row, "XP", chest.FixedValues?.Experience ?? 500, 0, 100000);
                void ApplyChest() => Edit(() => _editor.SetChestValues(
                    chest.Id, fixedValues.ButtonPressed ? new ChestValues((int)gold.Value, (int)experience.Value) : null));
                fixedValues.Toggled += _ => ApplyChest();
                gold.ValueChanged += _ => ApplyChest();
                experience.ValueChanged += _ => ApplyChest();
                break;
            }

            case MonsterGuard monster:
            {
                var row = new HBoxContainer();
                AddChild(row);
                var creature = Options(row, "Creature", creatures, monster.Creature);
                var count = Spin(row, "Count (0 = rolled)", monster.Count ?? 0, 0, 999);
                var row2 = new HBoxContainer();
                AddChild(row2);
                var disposition = Options(row2, "Disposition", Enum.GetValues<GuardDisposition>().Select(d => (d.ToString(), d)), monster.Disposition);
                var neverFlees = new CheckBox { Text = "Never flees", ButtonPressed = monster.NeverFlees };
                row2.AddChild(neverFlees);
                void ApplyMonster() => Edit(() => _editor.SetMonster(
                    monster.Id, creature.Value(), count.Value > 0 ? (int)count.Value : null, disposition.Value(), neverFlees.ButtonPressed));
                creature.Button.ItemSelected += _ => ApplyMonster();
                count.ValueChanged += _ => ApplyMonster();
                disposition.Button.ItemSelected += _ => ApplyMonster();
                neverFlees.Toggled += _ => ApplyMonster();

                var row3 = new HBoxContainer();
                AddChild(row3);
                var mode = Options(row3, "Guard mode", new[]
                {
                    ("Classic: zone of control, pickups can be stolen", GuardMode.Classic),
                    ("Area: treasure guard, the whole 3×3 area is guarded", GuardMode.Area),
                }, monster.Mode);
                mode.Button.ItemSelected += _ => Edit(() => _editor.SetMonsterGuardMode(monster.Id, mode.Value()));

                // A random monster (ADR-126): the creature above only shows it; the game start draws one of the tier.
                var row4 = new HBoxContainer();
                AddChild(row4);
                var tiers = new[] { ("Fixed creature", (int?)null), ("Random, any tier", 0) }
                    .Concat(Enumerable.Range(1, Core.Domain.Creatures.CreatureDefinition.MaxTier).Select(t => ($"Random tier {t}", (int?)t)));
                var randomTier = Options(row4, "Random", tiers, monster.RandomTier);
                randomTier.Button.ItemSelected += _ => Edit(() => _editor.SetMonsterRandomTier(monster.Id, randomTier.Value()));
                break;
            }

            case { TeleportTarget: var target } when mapObject is TwoWayGate or OneWayGateEntrance:
                AddChild(new Label { Text = target is { } t ? $"Linked to #{t.Value} (use the LinkGates tool to change)" : "Not linked (use the LinkGates tool)" });
                break;
            case ResourcePile pile:
                AddChild(new Label { Text = pile.Amount is { } fixedAmount ? $"{fixedAmount} {pile.Resource}" : $"{pile.Resource}, amount rolled at game start" });
                break;
            case Town town:
                ShowTown(town);
                break;
            case PandorasBox box:
                ShowBox(box, creatures);
                EventChoice("Runs the hero event", MapEventKind.Hero, box.Event, name => _editor.SetPandorasBoxEvent(box.Id, name));
                break;
            case QuestSite site:
                EventChoice("Quest", MapEventKind.Quest, site.Quest, name => _editor.SetQuestSiteQuest(site.Id, name));
                break;
        }
    }

    /// <summary>
    /// A row that picks one of the map's events of a kind by name (a box's hero event, a quest object's quest), or none; "New" writes a blank event
    /// of the kind and picks it (one undo step), "Edit" opens the picked one in the "Events" tab (<see cref="EventRequested"/>).
    /// </summary>
    private void EventChoice(string label, MapEventKind kind, string? current, Func<string?, EditResult> set)
    {
        var names = _editor.Scenario.MapEvents.Set.Events.Where(e => e.Kind == kind).Select(e => e.Name).ToList();
        if (current is not null && !names.Contains(current))
        {
            names.Add(current); // a name the map does not have: shown so it can be cleared (the validator or the loader names it)
        }

        var row = new HBoxContainer();
        AddChild(row);
        var choice = Options(row, label, new[] { ("— none —", "") }.Concat(names.Select(n => (n, n))), current ?? "");
        choice.Button.ItemSelected += _ => Edit(() => set(choice.Value().Length == 0 ? null : choice.Value()));
        var create = new Button { Text = "New" };
        create.Pressed += () => Edit(() => NewEvent(kind, set));
        row.AddChild(create);
        if (current is not null)
        {
            var open = new Button { Text = "Edit" };
            open.Pressed += () => EventRequested?.Invoke(current);
            row.AddChild(open);
        }
    }

    /// <summary>Adds a blank event of the kind and hands its name to <paramref name="use"/>, as one undo step.</summary>
    private EditResult NewEvent(MapEventKind kind, Func<string?, EditResult> use, IReadOnlyList<int>? towns = null)
    {
        var name = _editor.UnusedEventName(kind.ToString().ToLowerInvariant());
        _editor.BeginStroke($"New {kind.ToString().ToLowerInvariant()} event");
        try
        {
            var result = _editor.AddMapEvent(MapEventTemplates.Blank(name, kind, towns));
            return result == EditResult.Ok ? use(name) : result;
        }
        finally
        {
            _editor.EndStroke();
        }
    }

    /// <summary>The town events that name the town (each with "Edit"), and "New town event" for it.</summary>
    private void TownEvents(Town town)
    {
        var row = new HFlowContainer();
        AddChild(row);
        row.AddChild(new Label { Text = "Town events" });
        foreach (var definition in _editor.Scenario.MapEvents.Set.Events.Where(e => e.Kind == MapEventKind.Town && e.Towns.Contains(town.Id.Value)))
        {
            var name = definition.Name;
            var open = new Button { Text = name };
            open.Pressed += () => EventRequested?.Invoke(name);
            row.AddChild(open);
        }

        var create = new Button { Text = "New town event" };
        create.Pressed += () => Edit(() => NewEvent(MapEventKind.Town, name =>
        {
            EventRequested?.Invoke(name!);
            return EditResult.Ok;
        }, [town.Id.Value]));
        row.AddChild(create);
    }

    /// <summary>
    /// A Pandora's Box (ADR-100): what it gives, its guard and the message shown when it opens. Skills, artifacts, creatures and guard stacks list
    /// what the box has and one empty row to add another; picking "none" removes an entry.
    /// </summary>
    private void ShowBox(PandorasBox box, List<(string Text, string Id)> creatures)
    {
        var scenario = _editor.Scenario;
        var c = box.Contents;
        var changed = new List<Action<Action>>();
        AddChild(new Label { Text = "Gives (hidden from the players until opened; an empty box is an error)" });
        var resourceRow = new HFlowContainer();
        AddChild(resourceRow);
        var resources = Enum.GetValues<ResourceType>().Select(r => (Resource: r, Spin: Spin(resourceRow, r.ToString(), c.Resources[r], 0, 1000000))).ToList();
        var heroRow = new HFlowContainer();
        AddChild(heroRow);
        var experience = Spin(heroRow, "XP", c.Experience, 0, 1000000);
        var mana = Spin(heroRow, "Mana", c.Mana, 0, 999);
        var morale = Spin(heroRow, "Morale (next battle)", c.Morale, 0, PandorasBoxRules.MaxMoraleOrLuck);
        var luck = Spin(heroRow, "Luck (next battle)", c.Luck, 0, PandorasBoxRules.MaxMoraleOrLuck);
        var statRow = new HFlowContainer();
        AddChild(statRow);
        var stats = Enum.GetValues<PrimaryStat>().Select(s => (Stat: s, Spin: Spin(statRow, s.ToString(), c.Stat(s), 0, 99))).ToList();
        foreach (var spin in resources.Select(r => r.Spin).Concat(stats.Select(s => s.Spin)).Concat([experience, mana, morale, luck]))
        {
            changed.Add(apply => spin.ValueChanged += _ => apply());
        }

        AddChild(new Label { Text = "Skills (raised to at least this level)" });
        var skillItems = new[] { ("— none —", "") }.Concat(BuiltInSkills.All.OrderBy(s => s.Name).Select(s => (s.Name, s.Id))).ToList();
        var levelItems = Enum.GetValues<SkillLevel>().Where(l => l > SkillLevel.None).Select(l => (l.ToString(), l)).ToList();
        var skills = c.Skills.OrderBy(s => s.Key, StringComparer.Ordinal).Select(s => (s.Key, s.Value)).Append(("", SkillLevel.Basic)).Select(entry =>
        {
            var row = new HBoxContainer();
            AddChild(row);
            var skill = Options(row, "Skill", skillItems, entry.Item1);
            var level = Options(row, "at", levelItems, entry.Item2);
            changed.Add(apply => skill.Button.ItemSelected += _ => apply());
            changed.Add(apply => level.Button.ItemSelected += _ => apply());
            return (Skill: skill, Level: level);
        }).ToList();

        AddChild(new Label { Text = "Artifacts" });
        var artifactItems = new[] { ("— none —", "") }.Concat(scenario.Artifacts.All.OrderBy(a => a.Tier).ThenBy(a => a.Name).Select(a => ($"{a.Name} ({a.Tier})", a.Id))).ToList();
        var artifacts = c.Artifacts.Append("").Select(id =>
        {
            var row = new HBoxContainer();
            AddChild(row);
            var artifact = Options(row, "Artifact", artifactItems, id);
            changed.Add(apply => artifact.Button.ItemSelected += _ => apply());
            return artifact;
        }).ToList();

        var stackItems = new[] { ("— none —", "") }.Concat(creatures).ToList();
        List<(Choice<string> Creature, SpinBox Count)> StackRows(string title, IReadOnlyList<CreatureStack> stacks)
        {
            AddChild(new Label { Text = title });
            var shown = stacks.Count < Army.SlotCount ? stacks.Append(null) : stacks.Cast<CreatureStack?>();
            return shown.Select(stack =>
            {
                var row = new HBoxContainer();
                AddChild(row);
                var creature = Options(row, "", stackItems, stack?.Creature ?? "");
                var count = Spin(row, "×", stack?.Count ?? 1, 1, 99999);
                changed.Add(apply => creature.Button.ItemSelected += _ => apply());
                changed.Add(apply => count.ValueChanged += _ => apply());
                return (creature, count);
            }).ToList();
        }

        var joining = StackRows("Creatures that join the hero", c.Creatures);
        var guard = StackRows("Guard (fought after \"open?\"; none = unguarded)", box.Guard);

        var messageRow = new HBoxContainer();
        AddChild(messageRow);
        messageRow.AddChild(new Label { Text = "Message" });
        var message = new LineEdit { Text = box.Message ?? "", CustomMinimumSize = new Vector2(260, 0), PlaceholderText = "Shown when the box opens (optional)" };
        messageRow.AddChild(message);

        static List<CreatureStack> Read(List<(Choice<string> Creature, SpinBox Count)> rows) =>
            [.. rows.Where(r => r.Creature.Value().Length > 0).Select(r => new CreatureStack(r.Creature.Value(), (int)r.Count.Value))];

        void Apply() => Edit(() => _editor.SetPandorasBox(
            box.Id,
            new BoxContents
            {
                Resources = resources.Aggregate(ResourceSet.Empty, (sum, r) => sum + ResourceSet.Of(r.Resource, (int)r.Spin.Value)),
                Experience = (int)experience.Value,
                Mana = (int)mana.Value,
                Morale = (int)morale.Value,
                Luck = (int)luck.Value,
                Attack = (int)stats.First(s => s.Stat == PrimaryStat.Attack).Spin.Value,
                Defense = (int)stats.First(s => s.Stat == PrimaryStat.Defense).Spin.Value,
                SpellPower = (int)stats.First(s => s.Stat == PrimaryStat.SpellPower).Spin.Value,
                Knowledge = (int)stats.First(s => s.Stat == PrimaryStat.Knowledge).Spin.Value,
                Skills = skills.Where(s => s.Skill.Value().Length > 0).GroupBy(s => s.Skill.Value()).ToDictionary(g => g.Key, g => g.Last().Level.Value()),
                Artifacts = [.. artifacts.Select(a => a.Value()).Where(a => a.Length > 0)],
                Creatures = Read(joining),
            },
            Read(guard),
            message.Text));

        foreach (var hook in changed)
        {
            hook(Apply);
        }

        message.TextSubmitted += _ => Apply();
        message.FocusExited += Apply;
    }

    /// <summary>A sphinx's riddle (ADR-079): the author's question, answer and two wrong answers, or one drawn from the riddle table at start.</summary>
    private void ShowSphinx(Sphinx sphinx)
    {
        var own = new CheckBox { Text = "Author's riddle (else drawn from the riddle table)", ButtonPressed = sphinx.Riddle is not null };
        AddChild(own);
        LineEdit Field(string label, string value, string hint)
        {
            var row = new HBoxContainer();
            AddChild(row);
            row.AddChild(new Label { Text = label, CustomMinimumSize = new Vector2(90, 0) });
            var edit = new LineEdit { Text = value, CustomMinimumSize = new Vector2(280, 0), PlaceholderText = hint, Editable = sphinx.Riddle is not null };
            row.AddChild(edit);
            return edit;
        }

        var question = Field("Question", sphinx.Riddle?.Question ?? "", "What the sphinx asks");
        var answer = Field("Answer", sphinx.Riddle?.Answer ?? "", "The right answer");
        var wrong1 = Field("Wrong", sphinx.Riddle?.Wrong1 ?? "", "A wrong answer");
        var wrong2 = Field("Wrong", sphinx.Riddle?.Wrong2 ?? "", "Another wrong answer");
        LineEdit[] fields = [question, answer, wrong1, wrong2];

        void Apply()
        {
            var riddle = own.ButtonPressed
                ? new Riddle(question.Text.Trim(), answer.Text.Trim(), wrong1.Text.Trim(), wrong2.Text.Trim())
                : null;
            // Nothing changed (also when a field loses focus as the panel refreshes), or the author is still typing.
            if (riddle != sphinx.Riddle && (riddle is null || fields.All(f => f.Text.Trim().Length > 0)))
            {
                Edit(() => _editor.SetSphinxRiddle(sphinx.Id, riddle));
            }
        }

        own.Toggled += on =>
        {
            foreach (var field in fields)
            {
                field.Editable = on;
            }

            Apply();
        };
        foreach (var field in fields)
        {
            field.TextSubmitted += _ => Apply();
            field.FocusExited += Apply;
        }
    }

    /// <summary>Owner, name, pre-built buildings and dwellings (7-of-9 rule), fixed guild spells, garrison.</summary>
    private void ShowTown(Town town)
    {
        var scenario = _editor.Scenario;
        var row = new HBoxContainer();
        AddChild(row);
        var owners = new[] { ("Unowned", (PlayerId?)null) }.Concat(scenario.Players.Select(p => (p.Name, (PlayerId?)p.Id)));
        var owner = Options(row, "Owner", owners, town.Owner);
        owner.Button.ItemSelected += _ => Edit(() => _editor.SetTownOwner(town.Id, owner.Value()));
        row.AddChild(new Label { Text = "Name" });
        var name = new LineEdit { Text = town.Name, CustomMinimumSize = new Vector2(160, 0) };
        name.TextSubmitted += text => Edit(() => _editor.SetTownName(town.Id, text));
        row.AddChild(name);
        var type = Options(row, town.IsRandom ? "Shown as" : "Faction", BuiltInMapObjects.PlayableTowns.Select(t => (t, t)), town.TownType);
        type.Button.ItemSelected += _ => Edit(() => _editor.SetTownType(town.Id, type.Value()));
        var random = new CheckBox
        {
            Text = "Random faction",
            ButtonPressed = town.IsRandom,
            TooltipText = "Drawn when the game starts: an owned town takes its owner's chosen faction, a neutral one a random playable faction. " +
                          "Buildings carry over; dwellings move to the drawn faction's lines of the same tier.",
        };
        random.Toggled += on => Edit(() => _editor.SetTownRandom(town.Id, on));
        AddChild(random);

        AddChild(new Label { Text = "Buildings (level, 0 = not built)" });
        var buildings = new HFlowContainer();
        AddChild(buildings);
        var catalog = TownRules.Buildings(scenario, town);
        foreach (var building in catalog.TownBuildings)
        {
            var id = building;
            var level = Spin(buildings, building, town.Level(building), 0, catalog.MaxLevel(building));
            level.ValueChanged += value => Edit(() => _editor.SetTownBuilding(town.Id, id, (int)value));
        }

        AddChild(new Label { Text = "Dwellings (0 none, 1 built, 2 upgraded; at most 7, every tier reachable)" });
        var dwellings = new GridContainer { Columns = 2 };
        AddChild(dwellings);
        foreach (var line in TownRules.Lines(scenario, town))
        {
            var id = line.Id;
            var level = Spin(dwellings, $"T{line.Tier} {line.Name}", town.DwellingLevel(line.Id), 0, line.UpgradesTo is null ? 1 : 2);
            level.ValueChanged += value => Edit(() => _editor.SetTownDwelling(town.Id, id, (int)value));
        }

        ShowGuildSpells(town);
        GarrisonRows(town.Id, town.Garrison);
        TownEvents(town);
    }

    /// <summary>
    /// The spells the map fixes for the town's mage guild (map v30): per spell level as many choices as the guild offers there (3 / 3 / 2 / 2), each
    /// a spell of the town's school or "drawn" (the game start draws the rest). Towns without a school have no guild and no rows.
    /// </summary>
    private void ShowGuildSpells(Town town)
    {
        var spells = _editor.Scenario.Spells;
        if (town.IsRandom || !Enumerable.Range(1, MagicRules.GuildSpellsPerLevel.Count).Any(level => spells.Of(town.TownType, level).Count > 0))
        {
            return;
        }

        AddChild(new Label { Text = "Guild spells (— drawn — = drawn when the game starts)" });
        var choices = new List<Choice<string>>();
        for (var level = 1; level <= MagicRules.GuildSpellsPerLevel.Count; level++)
        {
            var items = new[] { ("— drawn —", "") }.Concat(spells.Of(town.TownType, level).OrderBy(s => s.Name).Select(s => (s.Name, s.Id))).ToList();
            var fixedHere = town.FixedGuildSpells.Where(s => spells.TryGet(s, out var spell) && spell.Level == level).ToList();
            var row = new HFlowContainer();
            AddChild(row);
            for (var slot = 0; slot < MagicRules.GuildSpellsPerLevel[level - 1]; slot++)
            {
                choices.Add(Options(row, slot == 0 ? $"L{level}" : "", items, fixedHere.ElementAtOrDefault(slot) ?? ""));
            }
        }

        void Apply() => Edit(() => _editor.SetTownGuildSpells(town.Id, [.. choices.Select(c => c.Value()).Where(s => s.Length > 0).Distinct()]));
        foreach (var choice in choices)
        {
            choice.Button.ItemSelected += _ => Apply();
        }
    }

    /// <summary>The seven garrison slots of a town or a map garrison (ADR-070).</summary>
    private void GarrisonRows(MapObjectId holder, Army troops)
    {
        AddChild(new Label { Text = "Garrison" });
        var creatures = new[] { ("— empty —", "") }
            .Concat(_editor.Scenario.Creatures.All.OrderBy(c => c.Town ?? "~").ThenBy(c => c.Tier).Select(c => ($"{c.Name} (T{c.Tier})", c.Id)))
            .ToList();
        for (var slot = 0; slot < Army.SlotCount; slot++)
        {
            var slotRow = new HBoxContainer();
            AddChild(slotRow);
            var stack = troops[slot];
            var creature = Options(slotRow, $"{slot + 1}", creatures, stack?.Creature ?? "");
            var count = Spin(slotRow, "×", stack?.Count ?? 1, 1, 9999);
            var index = slot;
            void ApplySlot() => Edit(() => _editor.SetGarrisonSlot(
                holder, index, creature.Value() is { Length: > 0 } id ? new CreatureStack(id, (int)count.Value) : null));
            creature.Button.ItemSelected += _ => ApplySlot();
            count.ValueChanged += _ => ApplySlot();
        }
    }

    /// <summary>Applies an edit unless the panel is just filling its fields.</summary>
    private void Edit(Func<EditResult> edit)
    {
        if (!_refreshing && edit() is not EditResult.Ok and var result)
        {
            GD.PushWarning($"Edit refused: {result}");
        }
    }

    private static SpinBox Spin(Container row, string label, int value, int min, int max)
    {
        row.AddChild(new Label { Text = label });
        var spin = new SpinBox { MinValue = min, MaxValue = max, Value = value };
        row.AddChild(spin);
        return spin;
    }

    private static Choice<T> Options<T>(Container row, string label, IEnumerable<(string Text, T Value)> items, T selected)
    {
        row.AddChild(new Label { Text = label });
        var list = items.ToList();
        var button = new OptionButton { FitToLongestItem = false, CustomMinimumSize = new Vector2(150, 0) };
        foreach (var (text, _) in list)
        {
            button.AddItem(text);
        }

        button.Selected = Math.Max(0, list.FindIndex(i => EqualityComparer<T>.Default.Equals(i.Value, selected)));
        row.AddChild(button);
        return new Choice<T>(button, list);
    }

    private sealed record Choice<T>(OptionButton Button, List<(string Text, T Value)> Items)
    {
        public T Value() => Items[Math.Max(0, Button.Selected)].Value;
    }
}
