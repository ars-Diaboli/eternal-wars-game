using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Decisions;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Godot.Shared.Events;
using EternalWars.Godot.Shared.Map;
using Godot;

namespace EternalWars.Godot.UI.Battle;

/// <summary>
/// Top-down debug view of the current battle (2D harness): grid, obstacles, units with their counts, the active
/// unit and, for a human-commanded unit, where it can move; during the deployment step (ADR-072) the deployer's zone. Shown while a battle runs, hidden afterwards.
/// Clicks are reported through <see cref="CellClicked"/>; the app maps them to battle commands.
/// </summary>
public partial class BattleBoard2D : Control
{
    private static readonly Color Grid = new(1, 1, 1, 0.08f);
    private static readonly Color Neutral = new(0.55f, 0.55f, 0.55f);
    private static readonly Color Reach = new(1, 1, 1, 0.12f);
    private static readonly Color Zone = new(0.35f, 0.65f, 1.0f, 0.18f);

    private GameSession _session = null!;

    public int CellSize { get; set; } = 22;

    /// <summary>A cell of the battlefield was clicked.</summary>
    public event Action<CellPosition>? CellClicked;

    public void Bind(GameSession session)
    {
        _session = session;
        Visible = false;
        MouseFilter = MouseFilterEnum.Stop;

        this.SubscribeWhileInTree<BattleStarted>(session.Events, e =>
        {
            CustomMinimumSize = new Vector2(e.Width * CellSize, e.Height * CellSize);
            Size = CustomMinimumSize;
            Visible = true;
            QueueRedraw();
        });
        this.SubscribeWhileInTree<DecisionResolved>(session.Events, e =>
        {
            if (e.Decision is BattleDecision)
            {
                Visible = false;
            }
        });

        this.SubscribeWhileInTree<BattleTurnStarted>(session.Events, _ => QueueRedraw());
        this.SubscribeWhileInTree<BattleUnitMoved>(session.Events, _ => QueueRedraw());
        this.SubscribeWhileInTree<BattleDeploymentStarted>(session.Events, _ => QueueRedraw());
        this.SubscribeWhileInTree<BattleUnitDeployed>(session.Events, _ => QueueRedraw());
        this.SubscribeWhileInTree<BattleDeploymentEnded>(session.Events, _ => QueueRedraw());
        this.SubscribeWhileInTree<BattleUnitAttacked>(session.Events, _ => QueueRedraw());
        this.SubscribeWhileInTree<BattleUnitDied>(session.Events, _ => QueueRedraw());
    }

    public override void _Draw()
    {
        if (_session.State.Battle is not { Phase: BattlePhase.Fighting or BattlePhase.Deploying } battle)
        {
            return;
        }

        DrawRect(new Rect2(Vector2.Zero, new Vector2(battle.Width, BattleState.Height) * CellSize), new Color(0.12f, 0.13f, 0.12f));
        for (var x = 0; x <= battle.Width; x++)
        {
            DrawLine(new Vector2(x * CellSize, 0), new Vector2(x * CellSize, BattleState.Height * CellSize), Grid);
        }

        for (var y = 0; y <= BattleState.Height; y++)
        {
            DrawLine(new Vector2(0, y * CellSize), new Vector2(battle.Width * CellSize, y * CellSize), Grid);
        }

        if (battle is { Phase: BattlePhase.Deploying, Deployer: { } deployer })
        {
            var from = deployer == BattleSide.Attacker ? 0 : battle.Width - battle.DeploymentColumns;
            DrawRect(ToRect(new CellRect(from, 0, battle.DeploymentColumns, BattleState.Height)), Zone);
        }
        else if (battle.ActiveUnit is { } active && _session.Battles.Commander(active.Side) is not null)
        {
            foreach (var placement in BattleGrid.Reachable(battle, active).Keys)
            {
                DrawRect(ToRect(new CellRect(placement, 1)), Reach);
            }
        }

        foreach (var obstacle in battle.Obstacles)
        {
            DrawRect(ToRect(obstacle.Rect).Grow(-1), new Color(0.35f, 0.30f, 0.25f));
        }

        var font = ThemeDB.FallbackFont;
        foreach (var unit in battle.LivingUnits)
        {
            var rect = ToRect(unit.Rect).Grow(-2);
            var owner = battle.Participant(unit.Side).Owner;
            DrawRect(rect, owner is { } player ? Palette.ForPlayer(player) : Neutral);
            if (unit == battle.ActiveUnit)
            {
                DrawRect(rect.Grow(2), Colors.White, filled: false, width: 2);
            }

            var label = $"{unit.Creature.Name[..Math.Min(4, unit.Creature.Name.Length)]} {unit.Count}";
            DrawString(font, rect.Position + new Vector2(3, 14), label, fontSize: 12, modulate: Colors.Black);
        }
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click)
        {
            CellClicked?.Invoke(new CellPosition(Mathf.FloorToInt(click.Position.X / CellSize), Mathf.FloorToInt(click.Position.Y / CellSize)));
            AcceptEvent();
        }
    }

    private Rect2 ToRect(CellRect rect) =>
        new(new Vector2(rect.X, rect.Y) * CellSize, new Vector2(rect.Width, rect.Height) * CellSize);
}
