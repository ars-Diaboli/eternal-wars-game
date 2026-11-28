using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Decisions;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Info;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Godot.Shared.Battle;
using EternalWars.Godot.Shared.Controls;
using EternalWars.Godot.Shared.Events;
using Godot;

namespace EternalWars.Godot.UI.Hud;

/// <summary>
/// The question card (ADR-309) for pending decisions: chest reward, guard offers, map event messages, questions and full armies, the battle
/// width and quick combat. A picture of the kind of question on top (<see cref="QuestionPictures"/>), the question, then one button per
/// answer; more than <see cref="MaxButtons"/> answers (a town gate's towns) are a scrolling list of rows. Answers are commands; the card hides
/// itself when the decision is resolved. A right click on the question opens the information window of what it is about, on a skill, a
/// town or a stack to choose that one's (ADR-154).
/// </summary>
public partial class DecisionPanel : PanelContainer
{
    /// <summary>The most answers shown as a row of buttons; more are a list.</summary>
    public const int MaxButtons = 4;

    /// <summary>The picture's size on the card (the files are 1024 × 576).</summary>
    public static readonly Vector2 PictureSize = new(480, 270);

    /// <summary>Where the question pictures are, <c>q_&lt;id&gt;.png</c>.</summary>
    private const string PictureFolder = "res://shared/textures/ui/questions/";

    /// <summary>The list's tallest before it scrolls.</summary>
    private const float ListHeight = 240;

    private readonly Label _prompt = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart };
    private readonly HBoxContainer _buttons = new() { Alignment = BoxContainer.AlignmentMode.End }; // along the bottom, at the right (ADR-204)
    private readonly VBoxContainer _list = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill };
    private Button? _closer; // the answer that declines or confirms: it stays a button under a long list
    private readonly ScrollContainer _listScroll = new() { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, Visible = false };
    private readonly PanelContainer _picture = new() { Name = "Picture", CustomMinimumSize = new Vector2(0, 0) };
    private string? _pictureId;
    private GameSession _session = null!;
    private object? _shownDecision; // the question the panel was last refreshed with
    private Func<Core.Domain.Common.PlayerId, bool> _playsHere = null!;

    /// <summary>The dialogue being read (ADR-242) and the line shown; a new dialogue starts at its first line.</summary>
    private MapEventDecision? _dialogue;
    private int _line;

    /// <summary>
    /// <paramref name="playsHere"/> says whose questions are asked at this screen (ADR-192): every human's by default; a battle's stays shown, its
    /// buttons are the battle's own.
    /// </summary>
    public void Bind(GameSession session, Func<Core.Domain.Common.PlayerId, bool>? playsHere = null)
    {
        _session = session;
        _playsHere = playsHere ?? (player => session.State.GetPlayer(player).IsHuman);
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", UiSpacing.Gap);
        AddChild(box);
        _picture.AddThemeStyleboxOverride("panel", UiTheme.Box(QuestionSlate, Colors.Transparent, 0, borderWidth: 0));
        _picture.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        box.AddChild(_picture);
        box.AddChild(_prompt.WithInfo(detailed => _session.State.PendingDecision is { } decision
            ? InfoSheets.Decision(_session.State, _session.State.CurrentPlayer.Id, decision, detailed)
            : null));
        _list.AddThemeConstantOverride("separation", UiSpacing.Cell);
        _listScroll.AddChild(_list);
        box.AddChild(_listScroll);
        box.AddChild(_buttons);
        Visible = false;

        this.SubscribeWhileInTree<DecisionRequested>(session.Events, _ => Refresh());
        this.SubscribeWhileInTree<BattleStarted>(session.Events, _ => Refresh());
        this.SubscribeWhileInTree<BattleRoundStarted>(session.Events, _ => Refresh());
        this.SubscribeWhileInTree<DecisionResolved>(session.Events, _ => Refresh());
        // A question resolved in another person's command of a simultaneous day (a defeat) is seen once the game is back in the shown turn (ADR-366).
        this.SubscribeWhileInTree<CommandExecuted>(session.Events, _ =>
        {
            if (!ReferenceEquals(_session.State.PendingDecision, _shownDecision))
            {
                Refresh();
            }
        });
        Refresh(); // a loaded game may already be waiting for an answer
    }

    private void Refresh()
    {
        // Another person's command of a simultaneous day: its battle and question are theirs, this screen keeps the one it shows (ADR-366).
        if (!_session.InShownTurn)
        {
            return;
        }

        _shownDecision = _session.State.PendingDecision;
        _buttons.ClearChildren(detach: true);
        _list.ClearChildren(detach: true);
        _closer = null;

        var state = _session.State;
        var player = state.CurrentPlayer.Id;
        Visible = state.PendingDecision is BattleDecision || (state.PendingDecision is not null && _playsHere(player));
        if (state.PendingDecision is not MapEventDecision { Dialogue.Count: > 0 })
        {
            _dialogue = null;
        }

        switch (state.PendingDecision)
        {
            case TreasureChestDecision chest:
                _prompt.Text = $"Treasure chest: take the gold or {chest.Experience} experience?";
                Button("Gold", new ResolveTreasureChestCommand(player, TreasureChestReward.Gold)).WithCost(ResourceSet.Of(ResourceType.Gold, chest.Gold));
                Button("Experience", new ResolveTreasureChestCommand(player, TreasureChestReward.Experience));
                break;
            case CartographerDecision cartographer:
                _prompt.Text = $"The cartographer sells the map of the {(cartographer.Level == 0 ? "surface" : "underground")}.";
                Button("Buy", new AnswerCartographerCommand(player, Buy: true)).WithCost(ResourceSet.Of(ResourceType.Gold, cartographer.Price));
                Button("Leave", new AnswerCartographerCommand(player, Buy: false));
                break;
            case DepositDecision deposit:
                _prompt.Text = $"Open the {_session.State.GetObject(deposit.Deposit).Definition.Name.ToLowerInvariant()} as a mine?";
                Button("Pay", new AnswerDepositCommand(player, Buy: true)).WithCost(deposit.Price);
                Button("Leave", new AnswerDepositCommand(player, Buy: false));
                break;
            case SphinxDecision sphinx:
                _prompt.Text = $"The sphinx asks: {sphinx.Question}";
                for (var i = 0; i < sphinx.Options.Count; i++)
                {
                    Button(sphinx.Options[i], new AnswerSphinxCommand(player, i));
                }

                break;
            case PandorasBoxDecision box:
                // An arena map's day of preparation shows what it holds (ARN-2 c).
                _prompt.Text = Core.Domain.Magic.AdventureIntel.ArenaContents(state, state.GetObject(box.Box)) is { Count: > 0 } shown
                    ? $"A Pandora's box holding {string.Join("; ", shown)}. Open it?"
                    : "A Pandora's box. Nobody knows what waits inside, or what guards it. Open it?";
                Button("Open", new AnswerPandorasBoxCommand(player, Open: true));
                Button("Leave", new AnswerPandorasBoxCommand(player, Open: false));
                break;
            case WitchHutDecision hut:
                _prompt.Text = $"The witch offers to teach {state.GetHero(hut.Hero).Name} one skill (one witch hut lesson per level).";
                foreach (var skill in hut.Options)
                {
                    var level = state.GetHero(hut.Hero).Skills.Level(skill) + 1;
                    Button($"{BuiltInSkills.Get(skill).Name} ({level})", new ChooseWitchHutSkillCommand(player, skill))
                        .WithSkillIcon(skill)
                        .WithInfo(detailed => InfoSheets.SkillChoice(_session.State.GetHero(hut.Hero), skill, detailed));
                }

                _closer = Button("Leave", new ChooseWitchHutSkillCommand(player, null));
                break;
            case TownGateDecision gate:
                _prompt.Text = "Town gate: go to which town?";
                foreach (var town in gate.Towns)
                {
                    Button(town.Available ? town.Name : $"{town.Name} (entrance blocked)", new ChooseTownGateCommand(player, town.Town), disabled: !town.Available)
                        .WithInfo(detailed => _session.State.FindObject(town.Town) is Core.Domain.Towns.Town shown
                            ? InfoSheets.Town(_session.State, player, shown, detailed)
                            : null);
                }

                _closer = Button("Stay", new ChooseTownGateCommand(player, null));
                break;
            case TrainingDecision training:
                var site = (Core.Domain.Objects.TrainingSite)state.GetObject(training.Site);
                _prompt.Text = $"{site.Definition.Name}: {state.GetHero(training.Hero).Name} may train once here.";
                foreach (var option in training.Options)
                {
                    var price = Core.Domain.Objects.TrainingRules.Price(site.Rule, option);
                    if (Core.Domain.Objects.TrainingRules.Stat(option) is { } trained)
                    {
                        Button($"+{Core.Domain.Objects.TrainingRules.StatBonus} {StatName(trained)}", new ChooseTrainingCommand(player, option))
                            .WithStatIcon(trained);
                    }
                    else
                    {
                        Button("Pay", new ChooseTrainingCommand(player, option)).WithCost(price);
                    }
                }

                if (training.CanDecline)
                {
                    _closer = Button("Leave", new ChooseTrainingCommand(player, null));
                }

                break;
            case MapEventDecision { Dialogue.Count: > 0 } dialogue:
                ShowDialogue(dialogue, player);
                break;
            case MapEventDecision message:
                // A map event's message (its reward lines, given once read) or question (EVT-4 f).
                _prompt.Text = message.Lines.Count == 0 ? message.Text : message.Text + "\n\n" + string.Join('\n', message.Lines);
                if (message.IsQuestion)
                {
                    for (var i = 0; i < message.Answers.Count; i++)
                    {
                        Button(message.Answers[i], new AnswerMapEventCommand(player, i));
                    }
                }
                else
                {
                    Button("OK", new AnswerMapEventCommand(player, 0));
                }

                break;
            case MapEventArmyDecision army:
                ShowArmyChoice(army, player);
                break;
            case HeroExchangeDecision:
                Visible = false; // ExchangePanel handles it
                break;
            case GuardEncounterDecision guard:
                var creature = state.Creatures.TryGet(guard.Creature, out var definition) ? definition.Name : guard.Creature;
                _prompt.Text = guard.Offer == GuardOffer.Join
                    ? $"{guard.Count} × {creature} offer to join you."
                    : $"{guard.Count} × {creature} try to flee.";
                Button(guard.Offer == GuardOffer.Join ? "Accept" : "Let them go", new RespondToGuardCommand(player, true))
                    .WithInfo(detailed => InfoSheets.Decision(_session.State, player, guard, detailed));
                Button("Fight", new RespondToGuardCommand(player, false))
                    .WithInfo(detailed => InfoSheets.Decision(_session.State, player, guard, detailed));
                break;
            case BattleDecision when state.Battle is { } battle:
                ShowBattle(battle);
                break;
            case LevelUpDecision levelUp:
                var hero = state.GetHero(levelUp.Hero);
                _prompt.Text = $"{hero.Name} reached level {levelUp.Level}: +1 {StatName(levelUp.Stat)}. Choose a skill:";
                foreach (var skill in levelUp.Options)
                {
                    var skillDefinition = BuiltInSkills.Get(skill);
                    Button($"{skillDefinition.Name} → {hero.Skills.Level(skill) + 1}", new ChooseSkillCommand(player, skill))
                        .WithSkillIcon(skill)
                        .WithInfo(detailed => InfoSheets.SkillChoice(_session.State.GetHero(levelUp.Hero), skill, detailed));
                }

                break;
        }

        ShowPicture(state.PendingDecision is { } pending ? QuestionPictures.Of(state, pending) : null);
        ListLongAnswers();
    }

    /// <summary>The card's slate, which the pictures' faded edges meet (RGB 28, 31, 38).</summary>
    private static readonly Color QuestionSlate = new(28 / 255f, 31 / 255f, 38 / 255f);

    /// <summary>
    /// The picture of the kind of question, <paramref name="id"/>'s file; until the file exists, a low slate box with the picture's name. No
    /// picture (null): the card has none.
    /// </summary>
    private void ShowPicture(string? id)
    {
        _picture.Visible = id is not null;
        if (id == _pictureId)
        {
            return;
        }

        _pictureId = id;
        _picture.ClearChildren(detach: true);
        if (id is null)
        {
            return;
        }

        var path = PictureFolder + id + ".png";
        if (ResourceLoader.Exists(path) && ResourceLoader.Load<Texture2D>(path) is { } texture)
        {
            _picture.AddChild(new TextureRect
            {
                Texture = texture, CustomMinimumSize = PictureSize, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = MouseFilterEnum.Ignore,
            });
            return;
        }

        // A placeholder until the question pictures are painted (step 5 of the HUD plan): low, so the card does not stand empty.
        var name = id.StartsWith("q_", StringComparison.Ordinal) ? id[2..] : id;
        _picture.AddChild(new Label
        {
            Text = char.ToUpperInvariant(name[0]) + name[1..].Replace('-', ' '), ThemeTypeVariation = UiTheme.QuietLabel,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            CustomMinimumSize = new Vector2(PictureSize.X, 72), MouseFilter = MouseFilterEnum.Ignore,
        });
    }

    /// <summary>More than <see cref="MaxButtons"/> answers: they move into the scrolling list, one row each, the text at the left; the
    /// answer that declines or confirms (Leave, Stay, Keep) stays a button below it.</summary>
    private void ListLongAnswers()
    {
        var answers = _buttons.GetChildren().OfType<Control>().ToList();
        _listScroll.Visible = answers.Count > MaxButtons;
        if (!_listScroll.Visible)
        {
            return;
        }

        foreach (var answer in answers.Where(a => a != _closer))
        {
            _buttons.RemoveChild(answer);
            answer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            if (answer is Button row)
            {
                row.Alignment = HorizontalAlignment.Left;
            }

            _list.AddChild(answer);
        }

        _listScroll.CustomMinimumSize = new Vector2(0, Math.Min(ListHeight, _list.GetCombinedMinimumSize().Y));
    }

    private static string StatName(PrimaryStat stat) => stat switch
    {
        PrimaryStat.SpellPower => "Spell Power",
        _ => stat.ToString(),
    };

    private void ShowBattle(BattleState battle)
    {
        var commander = BattleView3D.QuickCombatOf(_session, _playsHere);
        if (battle.Phase == BattlePhase.ChoosingWidth && battle.WidthChooser is { } side && _session.Battles.Commander(side) is { } chooser && _playsHere(chooser))
        {
            _prompt.Text = "Your Tactics lets you choose the battlefield width:";
            foreach (var width in battle.WidthOptions)
            {
                Button(width.ToString(), new ChooseBattleWidthCommand(chooser, width));
            }
        }
        else if (new[] { BattleSide.Attacker, BattleSide.Defender }.Select(_session.Battles.Commander).OfType<Core.Domain.Common.PlayerId>().ToList()
                 is { Count: > 0 } people && !people.Any(_playsHere))
        {
            // A LAN battle of other computers (ADR-195): its players fight it; this screen only waits. In a tournament it says nothing: the players
            // learn nothing of each other before their battle (ADR-366).
            if (_session.State.Rules.Tournament)
            {
                Visible = false;
                return;
            }

            _prompt.Text = $"{string.Join(" and ", people.Select(p => _session.State.GetPlayer(p).Name))} {(people.Count == 1 ? "fights" : "fight")} a battle on another computer.";
        }
        else
        {
            _prompt.Text = $"Battle, round {battle.Round}.";
        }

        if (commander is { } player)
        {
            Button("Quick combat", new AutoResolveBattleCommand(player));
        }
    }

    /// <summary>A map event gave a full army more creatures (EVT-4 e): tick the stacks to keep, one to seven; the rest stay behind.</summary>
    private void ShowArmyChoice(MapEventArmyDecision army, Core.Domain.Common.PlayerId player)
    {
        _prompt.Text = $"{_session.State.GetHero(army.Hero).Name}'s army is full. Keep which stacks (up to {Core.Domain.Armies.Army.SlotCount})?";
        var ticks = army.Stacks.Select((stack, i) => new CheckBox
        {
            Text = $"{stack.Count} × {(_session.State.Creatures.TryGet(stack.Creature, out var creature) ? creature.Name : stack.Creature)}",
            ButtonPressed = i < Core.Domain.Armies.Army.SlotCount,
        }.WithInfo(detailed => _session.State.Creatures.Contains(stack.Creature)
            ? InfoSheets.Creature(_session.State, stack.Creature, detailed, InfoSheets.Known(stack))
            : null)).ToList();
        var keep = new Button { Text = "Keep" };
        void Check() => keep.Disabled = ticks.Count(t => t.ButtonPressed) is < 1 or > Core.Domain.Armies.Army.SlotCount;
        foreach (var tick in ticks)
        {
            tick.Toggled += _ => Check();
            _buttons.AddChild(tick);
        }

        keep.Pressed += () => _session.Execute(new ChooseEventArmyCommand(player, [.. ticks.Select((t, i) => (t, i)).Where(x => x.t.ButtonPressed).Select(x => x.i)]));
        _buttons.AddChild(keep);
        _closer = keep;
        Check();
    }

    /// <summary>
    /// A map event's dialogue (ADR-242): one line at a time, its speaker above it; "Next" shows the next line here, "OK" on the last answers the
    /// event so it goes on.
    /// </summary>
    private void ShowDialogue(MapEventDecision dialogue, Core.Domain.Common.PlayerId player)
    {
        if (!Equals(dialogue, _dialogue))
        {
            (_dialogue, _line) = (dialogue, 0);
        }

        var line = dialogue.Dialogue[Math.Min(_line, dialogue.Dialogue.Count - 1)];
        var count = dialogue.Dialogue.Count > 1 ? $"\n\n({_line + 1} / {dialogue.Dialogue.Count})" : "";
        _prompt.Text = (line.Speaker.Length > 0 ? $"{line.Speaker}:\n" : "") + line.Text + count;
        if (_line < dialogue.Dialogue.Count - 1)
        {
            var next = new Button { Text = "Next" }.WithIcon(UiIcon.ArrowRight, keepText: true);
            next.Pressed += () =>
            {
                _line++;
                _buttons.ClearChildren();
                ShowDialogue(dialogue, player);
            };
            _buttons.AddChild(next);
            next.CallDeferred(Control.MethodName.GrabFocus);
        }
        else
        {
            Button("OK", new AnswerMapEventCommand(player, 0)).CallDeferred(Control.MethodName.GrabFocus);
        }
    }

    private Button Button(string text, GameCommand command, bool disabled = false)
    {
        var button = new Button { Text = text, Disabled = disabled };
        if (text is "OK" or "Accept")
        {
            button.WithIcon(UiIcon.Confirm, keepText: true);
        }

        button.Pressed += () => _session.Execute(command);
        _buttons.AddChild(button);
        return button;
    }
}
