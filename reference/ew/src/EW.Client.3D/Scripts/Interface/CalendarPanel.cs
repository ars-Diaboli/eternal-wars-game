using EW.Scripts.Calendar;
using EW.Scripts.Messaging;
using Godot;

public partial class CalendarPanel : PanelContainer, IHandleEvent<NewDay>
{
    [Export] public Label CalendarLabel { get; set; }

    private bool _isSubscribed;

    public override void _Ready()
    {
        ResolveReferences();
        SetPlaceholder();
    }

    public void Handle(NewDay eventData)
    {
        CalendarLabel.Text = $"Day {eventData.CurrentDay} / Week {eventData.CurrentWeek} / Month {eventData.CurrentMonth}";
    }

    private void SetPlaceholder()
    {
        if (CalendarLabel == null)
        {
            GD.PushWarning($"{Name}: CalendarLabel is not assigned and could not be resolved.");
            return;
        }

        CalendarLabel.Text = "Day - / Week - / Month -";
    }

    private void ResolveReferences()
    {
        CalendarLabel ??= GetNodeOrNull<Label>("MarginContainer/CalendarLabel");
    }
}
