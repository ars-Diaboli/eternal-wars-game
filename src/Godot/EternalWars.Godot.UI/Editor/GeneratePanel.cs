using System.Globalization;
using EternalWars.Core.Domain.Objects;
using Godot;

namespace EternalWars.Godot.UI.Editor;

/// <summary>A template the editor can generate from: what the panel offers for it (its start slots are named by <see cref="SlotNames"/>).</summary>
public sealed record GeneratorTemplate(string Name, string Description, IReadOnlyList<int> Sizes, int MinPlayers, int MaxHeroes, IReadOnlyList<string> SlotNames);

/// <summary>
/// What the designer asked for: the template (index into the panel's list), size, seed, the number of players (the first slots), a town per slot
/// that has one picked (the others are drawn from the seed) and the hero limit (null = the template's).
/// </summary>
public sealed record GenerateRequest(int Template, int Size, ulong Seed, int Players, IReadOnlyDictionary<int, string> Towns, int? MaxHeroes);

/// <summary>
/// "Generate from template" (ADR-087, ADR-064): template, size, seed, players, a town per player and the hero limit. "Generate" hands the request to
/// the app, which generates a new editable map like "New" (a new undo history) or returns why it cannot (shown here). The app passes the last
/// request back when it rebuilds the panel, so the settings stay.
/// </summary>
public partial class GeneratePanel : VBoxContainer
{
    private const string Random = "Random";

    private readonly OptionButton _template = new();
    private readonly Label _description = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(440, 0) };
    private readonly OptionButton _size = new();
    private readonly LineEdit _seed = new() { CustomMinimumSize = new Vector2(220, 0) };
    private readonly SpinBox _players = new() { MinValue = 1, MaxValue = 8, Step = 1 };
    private readonly SpinBox _maxHeroes = new() { MinValue = 1, MaxValue = 8, Step = 1, Prefix = "at most", Suffix = "heroes per player", CustomMinimumSize = new Vector2(260, 0) };
    private readonly GridContainer _towns = new() { Columns = 2 };
    private readonly Label _message = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(440, 0) };
    private readonly List<OptionButton> _townChoices = [];
    private IReadOnlyList<GeneratorTemplate> _templates = [];
    private Func<GenerateRequest, string?> _generate = _ => null;

    /// <summary>
    /// Binds the templates and the generate callback (it returns null when a map was generated, else the reason). <paramref name="last"/> is the
    /// previous request, restored in the controls.
    /// </summary>
    public void Bind(IReadOnlyList<GeneratorTemplate> templates, Func<GenerateRequest, string?> generate, GenerateRequest? last = null)
    {
        _templates = templates;
        _generate = generate;
        AddChild(new Label { Text = "Generate a new map from a template. It replaces the open map (save it first).", AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(440, 0) });
        foreach (var template in templates)
        {
            _template.AddItem(template.Name);
        }

        _template.ItemSelected += _ => ShowTemplate(null);
        Row("Template", _template);
        AddChild(_description);
        Row("Size", _size);
        _seed.Text = (last?.Seed ?? (ulong)System.Random.Shared.NextInt64()).ToString(CultureInfo.InvariantCulture);
        var reroll = new Button { Text = "New seed" };
        reroll.Pressed += () => _seed.Text = ((ulong)System.Random.Shared.NextInt64()).ToString(CultureInfo.InvariantCulture);
        Row("Seed", _seed, reroll);
        _players.ValueChanged += _ => ShowTowns(null);
        Row("Players", _players);
        Row("Hero limit", _maxHeroes);
        AddChild(new Label { Text = "Towns (a start zone is painted in its town's terrain):" });
        AddChild(_towns);
        var button = new Button { Text = "Generate" };
        button.Pressed += Generate;
        AddChild(button);
        AddChild(_message);

        if (templates.Count > 0)
        {
            _template.Select(last is { } request && request.Template < templates.Count ? request.Template : 0);
            ShowTemplate(last);
        }
    }

    /// <summary>Shows a message under the button (the app's report on the last generation).</summary>
    public void ShowMessage(string text) => _message.Text = text;

    private GeneratorTemplate? Selected => _template.Selected >= 0 && _template.Selected < _templates.Count ? _templates[_template.Selected] : null;

    private void Row(string label, params Control[] controls)
    {
        var row = new HBoxContainer();
        row.AddChild(new Label { Text = label, CustomMinimumSize = new Vector2(100, 0) });
        foreach (var control in controls)
        {
            row.AddChild(control);
        }

        AddChild(row);
    }

    /// <summary>The template's sizes, player range and hero limit; <paramref name="last"/> restores a request for this template.</summary>
    private void ShowTemplate(GenerateRequest? last)
    {
        if (Selected is not { } template)
        {
            return;
        }

        _description.Text = template.Description;
        _size.Clear();
        foreach (var size in template.Sizes)
        {
            _size.AddItem($"{size}×{size}", size);
        }

        var sizeIndex = last is null ? -1 : template.Sizes.ToList().IndexOf(last.Size);
        _size.Select(sizeIndex >= 0 ? sizeIndex : template.Sizes.Count / 2);
        _players.MinValue = template.MinPlayers;
        _players.MaxValue = template.SlotNames.Count;
        _players.SetValueNoSignal(last?.Players ?? template.SlotNames.Count);
        _maxHeroes.Value = last?.MaxHeroes ?? template.MaxHeroes;
        ShowTowns(last);
    }

    /// <summary>A town choice per player: "Random" or a playable town.</summary>
    private void ShowTowns(GenerateRequest? last)
    {
        if (Selected is not { } template)
        {
            return;
        }

        var kept = _townChoices.Select(c => c.Selected).ToList();
        foreach (var child in _towns.GetChildren())
        {
            _towns.RemoveChild(child);
            child.QueueFree();
        }

        _townChoices.Clear();
        for (var slot = 0; slot < (int)_players.Value; slot++)
        {
            _towns.AddChild(new Label { Text = template.SlotNames[slot], CustomMinimumSize = new Vector2(100, 0) });
            var town = new OptionButton();
            town.AddItem(Random);
            foreach (var type in BuiltInMapObjects.PlayableTowns)
            {
                town.AddItem(type);
            }

            var picked = last?.Towns.GetValueOrDefault(slot) is { } lastTown ? BuiltInMapObjects.PlayableTowns.ToList().IndexOf(lastTown) + 1 : slot < kept.Count ? kept[slot] : 0;
            town.Select(Math.Max(0, picked));
            _towns.AddChild(town);
            _townChoices.Add(town);
        }
    }

    private void Generate()
    {
        if (Selected is not { } template)
        {
            return;
        }

        if (!ulong.TryParse(_seed.Text.Trim(), CultureInfo.InvariantCulture, out var seed))
        {
            _message.Text = "Enter a numeric seed.";
            return;
        }

        var towns = _townChoices.Select((choice, slot) => (choice.Selected, slot))
            .Where(c => c.Selected > 0)
            .ToDictionary(c => c.slot, c => BuiltInMapObjects.PlayableTowns[c.Selected - 1]);
        var maxHeroes = (int)_maxHeroes.Value == template.MaxHeroes ? (int?)null : (int)_maxHeroes.Value;
        _message.Text = _generate(new GenerateRequest(_template.Selected, _size.GetSelectedId(), seed, (int)_players.Value, towns, maxHeroes)) ?? "";
    }
}
