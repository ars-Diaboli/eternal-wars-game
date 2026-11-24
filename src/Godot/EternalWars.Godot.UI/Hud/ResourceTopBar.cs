using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Game;
using EternalWars.Godot.UI.Widgets;
using Godot;

namespace EternalWars.Godot.UI.Hud;

/// <summary>
/// The adventure map's top bar (ADR-309): the six resources (<see cref="ResourceBar"/>) in the top corner away from the minimap, within the
/// status bar's 16:9 width. It stands on its own layer over the full-screen panels, which stop <see cref="Height"/> short of the top; while
/// one is open (<see cref="Wide"/>) the bar runs across that width, so nothing of the map shows above the panel.
/// </summary>
public partial class ResourceTopBar : PanelContainer
{
    /// <summary>The bar's height: one line of the resource icons, as tall as the status bar.</summary>
    public const float Height = StatusBar.Height;

    private bool _wide;
    private bool _onRight;

    public ResourceTopBar()
    {
        Name = "TopBar";
        MouseFilter = MouseFilterEnum.Stop;
    }

    /// <summary>Across the status bar's width (a full-screen panel is open) rather than as wide as the resources.</summary>
    public bool Wide
    {
        get => _wide;
        set
        {
            _wide = value;
            Restyle();
            Place();
        }
    }

    /// <summary>In the top-right corner: the mirrored layout, whose minimap is on the left (ADR-309).</summary>
    public bool OnRight
    {
        get => _onRight;
        set
        {
            _onRight = value;
            Restyle();
            Place();
        }
    }

    /// <param name="listed">Whose resources are shown: the current player's, or in a LAN game this screen's player's in another's turn (ADR-192).</param>
    public void Bind(GameSession session, Func<PlayerId> listed)
    {
        var resources = new ResourceBar { Name = "Resources" };
        AddChild(resources);
        resources.Bind(session, listed);
        Restyle();
        resources.MinimumSizeChanged += Place;
    }

    public override void _EnterTree()
    {
        GetViewport().SizeChanged += Place;
        Place();
    }

    public override void _ExitTree() => GetViewport().SizeChanged -= Place;

    /// <summary>The bar's place on <paramref name="screen"/>: <paramref name="width"/> wide in the top corner, or across the status bar's width.</summary>
    public static Rect2 Bounds(Vector2 screen, float width, bool wide, bool onRight)
    {
        var area = StatusBar.Bounds(screen, 0);
        if (wide)
        {
            return new Rect2(area.Position.X, 0, area.Size.X, Height);
        }

        width = Math.Min(width, area.Size.X);
        return new Rect2(onRight ? area.End.X - width : area.Position.X, 0, width, Height);
    }

    /// <summary>The status bar's look, its border on the edges that face the map.</summary>
    private void Restyle()
    {
        var style = UiTheme.Box(UiTheme.Background, UiTheme.Border, 2, borderWidth: 0);
        style.BorderWidthBottom = 2;
        style.BorderWidthLeft = !_wide && _onRight ? 2 : 0;
        style.BorderWidthRight = !_wide && !_onRight ? 2 : 0;
        style.ContentMarginLeft = style.ContentMarginRight = UiSpacing.Gap;
        _frame ??= UiFrames.Bevel(this, BevelKind.Panel, UiFrames.Bar(UiSpacing.Gap) ?? (StyleBox)style); // the HUD's bevelled panel (ADR-310), once
    }

    private BevelFrame? _frame;

    private void Place()
    {
        if (!IsInsideTree())
        {
            return;
        }

        var bounds = Bounds(GetViewportRect().Size, GetCombinedMinimumSize().X, _wide, _onRight);
        Position = bounds.Position;
        Size = bounds.Size;
    }
}
