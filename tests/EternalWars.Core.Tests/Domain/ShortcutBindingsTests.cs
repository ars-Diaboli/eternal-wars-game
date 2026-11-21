using EternalWars.Core.Domain.Editing;

namespace EternalWars.Core.Tests.Domain;

/// <summary>The editor's shortcuts (ADR-275): <see cref="KeyChord"/> and <see cref="ShortcutBindings"/>.</summary>
public sealed class ShortcutBindingsTests
{
    private static readonly KeyChord Save = new("S", Ctrl: true);
    private static readonly KeyChord SaveAs = new("S", Ctrl: true, Shift: true);
    private static readonly KeyChord Undo = new("Z", Ctrl: true);

    private static readonly Dictionary<string, KeyChord?> Defaults = new()
    {
        ["file.save"] = Save, ["file.save-as"] = SaveAs, ["edit.undo"] = Undo, ["file.export"] = null,
    };

    [Fact]
    public void AChord_ReadsBackFromItsText_ModifiersInAnyOrder()
    {
        Assert.Equal("Ctrl+Shift+Alt+F5", new KeyChord("F5", true, true, true).Text);
        Assert.Equal(SaveAs, KeyChord.Parse(SaveAs.Text));
        Assert.Equal(SaveAs, KeyChord.Parse("shift + CTRL+S"));
        Assert.Equal(new KeyChord("Delete"), KeyChord.Parse("Delete"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("Ctrl")]
    [InlineData("Ctrl+")]
    [InlineData("Ctrl+Ctrl+S")]
    [InlineData("Super+S")]
    public void Parse_RefusesWhatIsNoChord(string? text) => Assert.Null(KeyChord.Parse(text));

    [Fact]
    public void Resolve_PutsTheUsersChangesOverTheDefaults_AndDropsUnknownCommands()
    {
        var f12 = new KeyChord("F12");
        var resolved = ShortcutBindings.Resolve(Defaults, new Dictionary<string, KeyChord?>
        {
            ["file.export"] = f12, ["edit.undo"] = null, ["gone.command"] = Save,
        });

        Assert.Equal(Defaults.Keys, resolved.Keys);
        Assert.Equal([Save, SaveAs, null, f12], resolved.Values);
    }

    [Fact]
    public void Resolve_TakesADefaultChordTheUserGaveAnotherCommand()
    {
        var resolved = ShortcutBindings.Resolve(Defaults, new Dictionary<string, KeyChord?> { ["file.export"] = Save });

        Assert.Equal(Save, resolved["file.export"]);
        Assert.Null(resolved["file.save"]);
        Assert.Empty(ShortcutBindings.Conflicts(resolved));
    }

    [Fact]
    public void Resolve_DropsChangesThatGiveOneChordToSeveralCommands()
    {
        var f12 = new KeyChord("F12");
        var resolved = ShortcutBindings.Resolve(Defaults, new Dictionary<string, KeyChord?>
        {
            ["file.export"] = f12, ["edit.undo"] = f12, ["file.save-as"] = Save,
        });

        Assert.Equal([null, Save, Undo, null], resolved.Values); // Save's default goes to Save as; the two F12s fall back to their defaults
        Assert.Empty(ShortcutBindings.Conflicts(resolved));
    }

    [Fact]
    public void Changes_KeepOnlyWhatDiffersFromTheDefaults_AndReadBackTheSame()
    {
        var current = new Dictionary<string, KeyChord?>(Defaults) { ["edit.undo"] = null, ["file.export"] = new KeyChord("E", Alt: true) };
        var changes = ShortcutBindings.Changes(Defaults, current);

        Assert.Equal(["edit.undo", "file.export"], changes.Keys.Order());
        Assert.Equal(current, ShortcutBindings.Resolve(Defaults, changes));
        Assert.Empty(ShortcutBindings.Changes(Defaults, Defaults));
    }

    [Fact]
    public void Conflicts_ListEveryChordSeveralCommandsShare_InOrder()
    {
        var bindings = new Dictionary<string, KeyChord?>
        {
            ["a"] = Save, ["b"] = Undo, ["c"] = Save, ["d"] = null, ["e"] = null, ["f"] = Undo, ["g"] = Save,
        };

        var conflicts = ShortcutBindings.Conflicts(bindings);

        Assert.Equal([Save, Undo], conflicts.Select(c => c.Chord));
        Assert.Equal(["a", "c", "g"], conflicts[0].Commands);
        Assert.Equal(["b", "f"], conflicts[1].Commands);
        Assert.Empty(ShortcutBindings.Conflicts(Defaults));
    }
}
