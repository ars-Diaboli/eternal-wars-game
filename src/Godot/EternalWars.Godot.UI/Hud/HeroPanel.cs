using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Artifacts;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Godot.Shared.Events;
using Godot;

namespace EternalWars.Godot.UI.Hud;

/// <summary>
/// Hero screen for a hero of the player whose turn it is: stats (with what artifacts add), mana, experience, specialty,
/// skills with their descriptions, the army (click a stack, then a slot to move it; "Split" moves half), the paper doll
/// and the backpack (click a worn artifact to take it off, a carried one to put it on), and war machines. Every change is
/// a command; refusals show below. Opened with <see cref="Open"/>; Previous / Next go through the player's heroes. With
/// <see cref="FullScreen"/> it fills its parent and scrolls; Escape or "Close" shut it.
/// </summary>
public partial class HeroPanel : PanelContainer
{
    private readonly Label _title = new();
    private readonly Label _stats = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(600, 0) };
    private readonly Label _specialty = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(600, 0) };
    private readonly VBoxContainer _skills = new();
    private readonly HBoxContainer _army = new();
    private readonly GridContainer _doll = new() { Columns = 4 };
    private readonly HFlowContainer _backpack = new();
    private readonly Label _machines = new();
    private readonly CheckBox _split = new() { Text = "Split (move half)" };
    private readonly Label _status = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(600, 0) };
    private GameSession _session = null!;
    private HeroId? _hero;
    private int? _pickedSlot;

    /// <summary>Fill the parent and scroll (the game app's hero screen) instead of shrinking to the content. Set before <see cref="Bind"/>.</summary>
    public bool FullScreen { get; set; }

    public event Action? Closed;

    /// <summary>The hero the screen shows, if it is open.</summary>
    public HeroId? CurrentHero => Visible ? _hero : null;

    public void Bind(GameSession session)
    {
        _session = session;
        var box = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        if (FullScreen)
        {
            SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            this.UseOpaqueBackground();
            var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, SizeFlagsHorizontal = SizeFlags.ExpandFill };
            AddChild(scroll);
            scroll.AddChild(box);
        }
        else
        {
            AddChild(box);
        }

        var header = new HBoxContainer();
        box.AddChild(header);
        var previous = new Button { Text = "<" };
        previous.Pressed += () => Step(-1);
        header.AddChild(previous);
        header.AddChild(_title);
        var next = new Button { Text = ">" };
        next.Pressed += () => Step(1);
        header.AddChild(next);
        var close = new Button { Text = "Close" };
        close.Pressed += Close;
        header.AddChild(close);

        box.AddChild(_stats);
        box.AddChild(_specialty);
        box.AddChild(new Label { Text = "Skills" });
        box.AddChild(_skills);
        box.AddChild(new Label { Text = "Army" });
        box.AddChild(_army);
        box.AddChild(_split);
        box.AddChild(_machines);
        box.AddChild(new Label { Text = "Worn artifacts (click to take off)" });
        box.AddChild(_doll);
        box.AddChild(new Label { Text = "Backpack (click to put on)" });
        box.AddChild(_backpack);
        box.AddChild(_status);
        Visible = false;

        this.SubscribeWhileInTree<ArtifactEquipped>(session.Events, _ => Refresh());
        this.SubscribeWhileInTree<ArtifactUnequipped>(session.Events, _ => Refresh());
        this.SubscribeWhileInTree<ArtifactGiven>(session.Events, _ => Refresh());
        this.SubscribeWhileInTree<ArtifactGained>(session.Events, _ => Refresh());
        this.SubscribeWhileInTree<ArtifactLost>(session.Events, _ => Refresh());
        this.SubscribeWhileInTree<ArmyChanged>(session.Events, _ => Refresh());
        this.SubscribeWhileInTree<HeroLeveledUp>(session.Events, _ => Refresh());
        this.SubscribeWhileInTree<HeroExperienceGained>(session.Events, _ => Refresh());
        this.SubscribeWhileInTree<HeroManaRestored>(session.Events, _ => Refresh());
        this.SubscribeWhileInTree<HeroManaSpent>(session.Events, _ => Refresh());
        this.SubscribeWhileInTree<SpellsLearned>(session.Events, _ => Refresh());
        this.SubscribeWhileInTree<SpellBookBought>(session.Events, _ => Refresh());
        this.SubscribeWhileInTree<PandorasBoxOpened>(session.Events, _ => Refresh()); // stats and skills
        this.SubscribeWhileInTree<HeroMovementRestored>(session.Events, _ => Refresh());
        this.SubscribeWhileInTree<HeroMoved>(session.Events, _ => Refresh());
        this.SubscribeWhileInTree<WarMachineBought>(session.Events, _ => Refresh());
        this.SubscribeWhileInTree<HeroDefeated>(session.Events, _ => Refresh());
        this.SubscribeWhileInTree<TurnStarted>(session.Events, _ => Refresh());
    }

    /// <summary>Shows a hero of the player whose turn it is.</summary>
    public void Open(HeroId hero)
    {
        _hero = hero;
        _pickedSlot = null;
        _status.Text = "";
        Refresh();
    }

    public void Close()
    {
        var wasOpen = _hero is not null;
        _hero = null;
        Visible = false;
        if (wasOpen)
        {
            Closed?.Invoke();
        }
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (Visible && @event is InputEventKey { Pressed: true, Keycode: Key.Escape })
        {
            Close();
            GetViewport().SetInputAsHandled();
        }
    }

    private void Step(int direction)
    {
        var state = _session.State;
        var heroes = state.HeroesOf(state.CurrentPlayer.Id).Select(h => h.Id).ToList();
        if (_hero is { } current && heroes.IndexOf(current) is var index and >= 0 && heroes.Count > 1)
        {
            Open(heroes[(index + direction + heroes.Count) % heroes.Count]);
        }
    }

    private void Refresh()
    {
        if (_hero is not { } id)
        {
            return;
        }

        var state = _session.State;
        var hero = state.HeroesOf(state.CurrentPlayer.Id).FirstOrDefault(h => h.Id == id);
        if (hero is null)
        {
            Close();
            return;
        }

        Visible = true;
        _title.Text = $"{hero.Name}, level {hero.Level} {hero.Class}" + (hero.Definition is { } d ? $" ({state.HeroRoster.Get(d).Town})" : "");
        _stats.Text = StatsText(hero);
        _specialty.Text = hero.Specialty is { } specialty ? $"Specialty: {SpecialtyText(specialty)}" : "No specialty.";
        ShowSkills(hero);
        ShowArmy(hero);
        ShowArtifacts(hero);
        _machines.Text = "War machines: " + (hero.WarMachines.Count == 0 ? "none" : string.Join(", ", hero.WarMachines.Order()));
    }

    private static string WithBonus(int baseValue, int bonus) =>
        bonus == 0 ? $"{baseValue}" : $"{baseValue + bonus} ({baseValue} {(bonus > 0 ? "+" : "−")} {Math.Abs(bonus)})";

    private static string Signed(int value) => value > 0 ? $"+{value}" : $"{value}";

    private static string StatsText(Hero hero)
    {
        var next = ExperienceTable.For(hero.Level + 1);
        var bonus = hero.Bonus;
        return string.Join("\n",
            $"Attack {WithBonus(hero.Attack, bonus.Attack)}   Defense {WithBonus(hero.Defense, bonus.Defense)}   Spell Power {WithBonus(hero.SpellPower, bonus.SpellPower)}   Knowledge {WithBonus(hero.Knowledge, bonus.Knowledge)}",
            $"Morale {Signed(hero.TotalMorale)}   Luck {Signed(hero.TotalLuck)}   Mana {hero.Mana}/{hero.MaxMana}   Movement {hero.MovementPoints}/{hero.MaxMovementPoints}",
            $"Experience {hero.Experience}/{next}" + (hero.PendingLevelUps > 0 ? $"   {hero.PendingLevelUps} level-up(s) waiting" : "") + $"   Scouting {hero.ScoutingRadius}",
            hero.Artifacts.IsEmpty ? "No artifacts." : $"Artifacts: {bonus.Describe()}",
            SpellsText(hero));
    }

    /// <summary>The learned spells by name, and how many more the worn artifacts teach (the Unwritten Codex, ADR-111).</summary>
    private static string SpellsText(Hero hero)
    {
        if (!hero.HasSpellBook)
        {
            return "No spell book.";
        }

        var lent = EternalWars.Core.Domain.Magic.MagicRules.Known(hero, EternalWars.Core.Domain.Magic.SpellCatalog.Default).Count() - hero.Spells.Count;
        var learned = hero.Spells.Count == 0 ? "Spell book: empty." : $"Spells: {string.Join(", ", hero.Spells.Keys.Select(SpellName))}";
        return lent > 0 ? $"{learned}   (+{lent} while its artifacts are worn)" : learned;
    }

    private static string SpellName(string spell) =>
        EternalWars.Core.Domain.Magic.SpellCatalog.Default.TryGet(spell, out var definition) ? definition.Name : spell;

    private static string SpecialtyText(HeroSpecialty specialty) => specialty.Kind switch
    {
        SpecialtyKind.Skill when BuiltInSkills.Contains(specialty.Target) => $"{BuiltInSkills.Get(specialty.Target).Name} (skill)",
        SpecialtyKind.Resource => $"{specialty.Amount} {specialty.Target} a day",
        _ => specialty.ToString(),
    };

    private void ShowSkills(Hero hero)
    {
        Clear(_skills);
        var any = false;
        foreach (var group in BuiltInSkills.Groups)
        {
            // Skills the hero learned, plus ones an artifact adds.
            var rows = BuiltInSkills.SkillsOf(group.Id).Where(s => hero.SkillLevelOf(s.Id) > SkillLevel.None).ToList();
            if (rows.Count == 0)
            {
                continue;
            }

            any = true;
            var points = rows.Sum(s => (int)hero.Skills.Level(s.Id));
            _skills.AddChild(new Label { Text = $"{group.Name} ({points}/{SkillGroup.Points})" });
            foreach (var skill in rows)
            {
                var level = hero.SkillLevelOf(skill.Id);
                var learned = hero.Skills.Level(skill.Id);
                _skills.AddChild(new Label
                {
                    Text = $"    {skill.Name}: {level}" + (level != learned ? $" (learned {learned})" : "") + $" of {skill.MaxLevel}\n        {skill.Description}",
                    AutowrapMode = TextServer.AutowrapMode.WordSmart,
                    CustomMinimumSize = new Vector2(600, 0),
                });
            }
        }

        if (!any)
        {
            _skills.AddChild(new Label { Text = "    No skills yet." });
        }
    }

    private void ShowArmy(Hero hero)
    {
        Clear(_army);
        for (var slot = 0; slot < Army.SlotCount; slot++)
        {
            var stack = hero.Army[slot];
            var button = new Button
            {
                Text = stack is null ? "—" : $"{stack.Count}\n{_session.State.Creatures.Get(stack.Creature).Name}",
                CustomMinimumSize = new Vector2(96, 56),
                ToggleMode = true,
                ButtonPressed = _pickedSlot == slot,
            };
            var target = slot;
            button.Pressed += () => ClickSlot(hero.Id, target);
            _army.AddChild(button);
        }
    }

    private void ClickSlot(HeroId id, int slot)
    {
        var hero = _session.State.GetHero(id);
        if (_pickedSlot is not { } from)
        {
            if (hero.Army[slot] is not null)
            {
                _pickedSlot = slot;
            }

            Refresh();
            return;
        }

        _pickedSlot = null;
        if (from != slot)
        {
            var stack = hero.Army[from]!;
            int? count = _split.ButtonPressed && stack.Count > 1 ? stack.Count / 2 : null;
            Run(new MoveStackCommand(_session.State.CurrentPlayer.Id, id, from, id, slot, count));
        }

        Refresh();
    }

    private void ShowArtifacts(Hero hero)
    {
        Clear(_doll);
        foreach (var slot in ArtifactSlots.All)
        {
            var worn = hero.Artifacts.Worn(slot);
            for (var i = 0; i < ArtifactSlots.Capacity(slot); i++)
            {
                var artifact = i < worn.Count ? _session.State.Artifacts.Get(worn[i]) : null;
                var button = new Button
                {
                    Text = $"{slot}: {artifact?.Name ?? "—"}",
                    TooltipText = artifact is null ? "" : ArtifactText(artifact),
                    Disabled = artifact is null,
                    CustomMinimumSize = new Vector2(190, 0),
                    ClipText = true,
                };
                if (artifact is not null)
                {
                    var id = artifact.Id;
                    button.Pressed += () => Run(new UnequipArtifactCommand(_session.State.CurrentPlayer.Id, hero.Id, id));
                }

                _doll.AddChild(button);
            }
        }

        Clear(_backpack);
        if (hero.Artifacts.Backpack.Count == 0)
        {
            _backpack.AddChild(new Label { Text = "    Empty." });
        }

        foreach (var id in hero.Artifacts.Backpack)
        {
            var artifact = _session.State.Artifacts.Get(id);
            var button = new Button { Text = $"{artifact.Name} ({artifact.Slot})", TooltipText = ArtifactText(artifact) };
            var artifactId = artifact.Id;
            button.Pressed += () => Run(new EquipArtifactCommand(_session.State.CurrentPlayer.Id, hero.Id, artifactId));
            _backpack.AddChild(button);
        }
    }

    private string ArtifactText(ArtifactDefinition artifact) =>
        $"{artifact.Name} ({artifact.Tier}, {artifact.Slot})\n{ArtifactBonuses.Of(artifact.Effects).Describe()}"
        + (artifact.Set is { } set ? $"\nSet: {_session.State.Artifacts.GetSet(set).Name}" : "")
        + (artifact.Notes.Length > 0 ? $"\n{artifact.Notes}" : "");

    private void Run(GameCommand command)
    {
        var result = _session.Execute(command);
        _status.Text = result.IsAccepted ? "" : $"Refused: {result.Error}";
    }

    private static void Clear(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            child.QueueFree();
        }
    }
}
