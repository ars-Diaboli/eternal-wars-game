using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Info;
using EternalWars.Core.Domain.Magic;
using EternalWars.Godot.Shared.Controls;
using EternalWars.Godot.UI.Hud;
using Godot;

namespace EternalWars.Godot.UI.Battle;

/// <summary>
/// The battle's spell book (ADR-314), a window over the battle opened from the action grid, the side plate's button or C: the hero's mana, its
/// battle spells by school with their rank and cost (<see cref="BattleSpellBook"/>), and the warcries it knows as a category of their own
/// (ADR-067). A spell or warcry that cannot be used now is greyed and says why on the status bar. Picking a spell closes the book and arms it
/// (<see cref="SpellPicked"/>); picking a warcry closes it and cries it (<see cref="WarcryPicked"/>). A right click on a spell opens its
/// information window. Escape, C or the close button close it. Redrawn only when what it shows changes.
/// </summary>
public partial class BattleSpellBookWindow : PanelContainer
{
    private readonly Label _title = new();
    private readonly VBoxContainer _body = new();
    private readonly ScrollContainer _scroll = new() { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, CustomMinimumSize = new Vector2(620, 0) };
    private GameSession _session = null!;
    private string? _shown;

    /// <summary>A spell was picked: the HUD arms it (or casts a spell for a side at once).</summary>
    public event Action<KnownSpell>? SpellPicked;

    /// <summary>A warcry was picked: the HUD cries it.</summary>
    public event Action<Warcry>? WarcryPicked;

    public void Bind(GameSession session)
    {
        _session = session;
        Name = "BattleSpellBook";
        this.CenterAsScreen();
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", UiSpacing.Gap);
        AddChild(box);
        var header = new HBoxContainer();
        _title.AddThemeFontSizeOverride("font_size", 20);
        header.AddChild(_title);
        header.AddCloseButton(Close);
        box.AddChild(header);
        _body.AddThemeConstantOverride("separation", UiSpacing.Cell);
        _body.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _scroll.AddChild(_body);
        box.AddChild(_scroll);
        Visible = false;
    }

    /// <summary>The book is open.</summary>
    public bool IsOpen => Visible;

    /// <summary>
    /// Opens (or redraws) the book of the hero named <paramref name="hero"/>; <paramref name="notNow"/> greys every row with its reason (not the
    /// side's turn, the replay viewer).
    /// </summary>
    public void Open(BattleSpellBook book, string hero, string? notNow)
    {
        var key = Key(book, hero, notNow);
        if (Visible && key == _shown)
        {
            return; // nothing changed: the rows, the scroll and a press under way stay
        }

        _shown = key;
        _title.Text = $"{hero}: spell book · mana {book.Mana}";
        _body.ClearChildren(detach: true);
        foreach (var school in book.Spells.GroupBy(s => s.SchoolName))
        {
            _body.AddHeading(school.Key);
            var row = new HFlowContainer();
            row.AddThemeConstantOverride("h_separation", UiSpacing.Cell);
            row.AddThemeConstantOverride("v_separation", UiSpacing.Cell);
            foreach (var spell in school)
            {
                var known = spell.Known;
                var button = new Button { Text = $"{MagicRules.NameAtRank(known.Spell, known.Rank)} ({spell.Cost})", FocusMode = FocusModeEnum.None };
                var refusal = notNow ?? spell.Refusal;
                button.Disabled = refusal is not null;
                button.TooltipText = refusal ?? known.Spell.Notes;
                button.Pressed += () =>
                {
                    Close();
                    SpellPicked?.Invoke(known);
                };
                var id = known.Spell.Id;
                var rank = known.Rank;
                button.WithSpellIcon(id).WithInfo(detailed => InfoSheets.Spell(_session.State, id, rank, detailed));
                row.AddChild(button);
            }

            _body.AddChild(row);
        }

        if (book.Warcries.Count > 0)
        {
            _body.AddHeading("Warcries");
            var row = new HFlowContainer();
            row.AddThemeConstantOverride("h_separation", UiSpacing.Cell);
            foreach (var (warcry, refusal) in book.Warcries)
            {
                var button = new Button { Text = $"{warcry} (warcry)", FocusMode = FocusModeEnum.None }.WithIcon(UiIcon.Warcry, keepText: true);
                var why = notNow ?? refusal;
                button.Disabled = why is not null;
                button.TooltipText = why ?? "Cried at once, for the whole army; one warcry a round";
                button.Pressed += () =>
                {
                    Close();
                    WarcryPicked?.Invoke(warcry);
                };
                row.AddChild(button);
            }

            _body.AddChild(row);
        }

        if (_body.GetChildCount() == 0)
        {
            _body.AddChild(new Label { Text = "No spells to cast in battle." });
        }

        Visible = true;
        FitHeight();
        Callable.From(FitHeight).CallDeferred(); // the wrapped rows know their height once laid out
    }

    /// <summary>As tall as its rows, up to most of the screen; the rest scrolls.</summary>
    private void FitHeight()
    {
        if (!IsInstanceValid(this) || !IsInsideTree())
        {
            return;
        }

        var room = GetViewportRect().Size.Y * 0.6f;
        _scroll.CustomMinimumSize = new Vector2(_scroll.CustomMinimumSize.X, Math.Min(room, _body.GetCombinedMinimumSize().Y));
    }

    public void Close() => Visible = false;

    /// <summary>What the book shows, to skip a redraw that would change nothing.</summary>
    private static string Key(BattleSpellBook book, string hero, string? notNow) =>
        string.Join('|', hero, notNow, book.Mana,
            string.Join(';', book.Spells.Select(s => $"{s.Known.Spell.Id.Value}:{s.Known.Rank}:{s.Cost}:{s.Refusal}")),
            string.Join(';', book.Warcries.Select(w => $"{w.Warcry}:{w.Refusal}")));
}
