using EternalWars.Core.Domain.Settings;
using static EternalWars.Core.Domain.Settings.PointerActionKind;

namespace EternalWars.Core.Tests.Domain;

/// <summary>A finger on a touch screen as the mouse (ADR-190): tap, drag, long press.</summary>
public class TouchGestureTests
{
    [Fact]
    public void ATap_IsALeftClickWhereTheFingerWentDown()
    {
        var touch = new TouchGesture();

        Assert.Equal([new PointerAction(Hover, 100, 200)], touch.Down(0, 100, 200, 1.0));
        Assert.Empty(touch.Move(0, 105, 203));
        Assert.Empty(touch.Tick(1.2));
        Assert.Equal([new PointerAction(LeftPress, 100, 200), new PointerAction(LeftRelease, 100, 200)], touch.Up(0, 105, 203));
    }

    [Fact]
    public void AFingerHeldStill_IsARightClick_AndLiftingItDoesNothingMore()
    {
        var touch = new TouchGesture();
        touch.Down(0, 100, 200, 1.0);

        Assert.Empty(touch.Tick(1.0 + TouchGesture.HoldSeconds - 0.01));
        Assert.Equal([new PointerAction(RightClick, 100, 200)], touch.Tick(1.0 + TouchGesture.HoldSeconds));
        Assert.Empty(touch.Tick(3.0));
        Assert.Empty(touch.Move(0, 300, 200));
        Assert.Empty(touch.Up(0, 300, 200));
    }

    [Fact]
    public void AFingerThatMoves_DragsWithTheLeftButtonFromWhereItWentDown_AndIsNeverALongPress()
    {
        var touch = new TouchGesture();
        touch.Down(0, 100, 200, 1.0);

        Assert.Equal([new PointerAction(LeftPress, 100, 200), new PointerAction(LeftDrag, 120, 200)], touch.Move(0, 120, 200));
        Assert.Empty(touch.Tick(5.0));
        Assert.Equal([new PointerAction(LeftDrag, 150, 210)], touch.Move(0, 150, 210));
        Assert.Equal([new PointerAction(LeftRelease, 150, 210)], touch.Up(0, 150, 210));
    }

    [Fact]
    public void ASecondFinger_CancelsAnUndecidedTouch_AndIsIgnored()
    {
        var touch = new TouchGesture();
        touch.Down(0, 100, 200, 1.0);

        Assert.Empty(touch.Down(1, 400, 200, 1.1));
        Assert.Empty(touch.Move(1, 500, 200));
        Assert.Empty(touch.Tick(2.0));
        Assert.Empty(touch.Up(1, 500, 200));
        Assert.Empty(touch.Up(0, 100, 200));
        Assert.Equal([new PointerAction(Hover, 10, 20)], touch.Down(1, 10, 20, 3.0)); // the next touch starts afresh
    }
}
