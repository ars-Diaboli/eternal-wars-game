using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Editing;
using Godot;

namespace EternalWars.Godot.UI.Editor.Tools;

/// <summary>A tool that does nothing until a subclass says otherwise: most tools answer one or two of the pointer's moves.</summary>
public abstract class EditorTool(EditorToolContext context) : IEditorTool
{
    public abstract string Name { get; }

    protected EditorToolContext Context { get; } = context;

    protected MapEditorSession Editor => Context.Editor;

    public virtual void Activate()
    {
    }

    public virtual void Hover(MapPosition? tile)
    {
    }

    public virtual void Press(MapPosition tile)
    {
    }

    public virtual void Drag(MapPosition tile)
    {
    }

    public virtual void Release()
    {
    }

    public virtual void Cancel()
    {
    }

    public virtual void BuildOptions(ToolOptions options)
    {
    }
}

/// <summary>A tool that paints while dragged: one undo step per stroke, a dab on every move of the pointer.</summary>
public abstract class BrushTool(EditorToolContext context) : EditorTool(context)
{
    private bool _stroking;

    /// <summary>The undo step a stroke makes.</summary>
    protected abstract string StrokeDescription { get; }

    public override void Press(MapPosition tile)
    {
        StartStroke(tile);
        _stroking = true;
        Editor.BeginStroke(StrokeDescription);
        Paint(tile);
    }

    public override void Drag(MapPosition tile)
    {
        if (_stroking)
        {
            Paint(tile);
        }
    }

    public override void Release()
    {
        if (_stroking)
        {
            _stroking = false;
            Editor.EndStroke();
        }
    }

    public override void Cancel() => Release();

    /// <summary>A stroke starts on <paramref name="tile"/>, before its undo step opens.</summary>
    protected virtual void StartStroke(MapPosition tile)
    {
    }

    /// <summary>One dab of the brush on <paramref name="tile"/>.</summary>
    protected abstract void Paint(MapPosition tile);

    /// <summary>The brushes' shared radius.</summary>
    protected void Radius(ToolOptions options)
    {
        var radius = new SpinBox { MinValue = 0, MaxValue = 5, Value = Context.Radius };
        radius.ValueChanged += v => Context.Radius = (int)v;
        options.Labelled("Radius", radius);
    }

    /// <summary>The session's Auto height switch: painting terrain also gives the ground the new terrain's height.</summary>
    protected void AutoHeight(ToolOptions options)
    {
        var auto = new CheckBox
        {
            Text = "Auto height",
            ButtonPressed = Editor.Heights.AutoHeight,
            TooltipText = "Painting terrain also gives the ground there the new terrain's height",
        };
        auto.Toggled += on => Editor.Heights.AutoHeight = on;
        options.Add(auto);
    }
}
