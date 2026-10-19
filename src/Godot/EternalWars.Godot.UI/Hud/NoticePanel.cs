using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Godot.Shared.Events;
using Godot;

namespace EternalWars.Godot.UI.Hud;

/// <summary>
/// A short message from the map for the player whose turn it is (ADR-068): a sign's text, why an object turned the hero away, an emptied
/// object, a freed prisoner. Hidden again by "OK", Escape or the hero's next step.
/// </summary>
public partial class NoticePanel : PanelContainer
{
    private readonly Label _text = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(360, 0) };
    private GameSession _session = null!;

    public void Bind(GameSession session)
    {
        _session = session;
        var box = new VBoxContainer();
        AddChild(box);
        box.AddChild(_text);
        var ok = new Button { Text = "OK" };
        ok.Pressed += () => Visible = false;
        box.AddChild(ok);
        Visible = false;

        this.SubscribeWhileInTree<SignRead>(session.Events, e => Show(e.Hero, $"The sign reads: \"{e.Message}\""));
        this.SubscribeWhileInTree<MapObjectRefused>(session.Events, e => Show(e.Hero, e.Reason));
        this.SubscribeWhileInTree<MapObjectFoundEmpty>(session.Events, e => Show(e.Hero, $"Nothing more to find at the {NameOf(e.Object)} for now."));
        this.SubscribeWhileInTree<HeroFreed>(session.Events, e => Show(e.Hero, $"{_session.State.GetHero(e.Hero).Name} (level {e.Level}) is free and joins you."));
        this.SubscribeWhileInTree<CreatureBankPlundered>(session.Events, e =>
            Show(e.Hero, $"The {NameOf(e.Bank)} is yours to plunder: {Core.Domain.Objects.ResourceProviderRules.Describe(e.Resources)}"
                         + (e.Artifact is { } artifact ? $" and {_session.State.Artifacts.Get(artifact).Name}." : ".")));
        this.SubscribeWhileInTree<HeroMoved>(session.Events, _ => Visible = false);
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (Visible && @event is InputEventKey { Pressed: true, Keycode: Key.Escape })
        {
            Visible = false;
            GetViewport().SetInputAsHandled();
        }
    }

    private string NameOf(Core.Domain.Common.MapObjectId id) =>
        _session.State.FindObject(id)?.Definition.Name.ToLowerInvariant() ?? "place";

    /// <summary>Only the player whose hero it is, and only a human, sees it.</summary>
    private void Show(Core.Domain.Common.HeroId hero, string text)
    {
        var state = _session.State;
        if (state.Heroes.FirstOrDefault(h => h.Id == hero) is not { } found || found.Owner != state.CurrentPlayer.Id || !state.CurrentPlayer.IsHuman)
        {
            return;
        }

        _text.Text = text;
        Visible = true;
    }
}
