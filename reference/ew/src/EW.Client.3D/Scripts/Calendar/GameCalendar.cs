using EW.Scripts.Messaging;
using Godot;

namespace EW.Scripts.Calendar;

public record CalendarEvent(int DaysPassed, int CurrentDay, int CurrentWeek, int CurrentMonth) : IGameEvent;
public record NewDay(int DaysPassed, int CurrentDay, int CurrentWeek, int CurrentMonth) 
    : CalendarEvent(DaysPassed, CurrentDay, CurrentWeek, CurrentMonth);
public record NewWeek(int DaysPassed, int CurrentDay, int CurrentWeek, int CurrentMonth) 
    : CalendarEvent(DaysPassed, CurrentDay, CurrentWeek, CurrentMonth);
public record NewMonth(int DaysPassed, int CurrentDay, int CurrentWeek, int CurrentMonth) 
    : CalendarEvent(DaysPassed, CurrentDay, CurrentWeek, CurrentMonth);
public sealed class DayAdvanced : IGameEvent;

public sealed partial class GameCalendar : Node, IHandleEvent<DayAdvanced>
{
    private const int DaysPerWeek = 7;
    private const int WeeksPerMonth = 4;

    public int DaysPassed { get; private set; } = 1;
    public int CurrentDay => DaysPassed % DaysPerWeek;
    public int CurrentWeek => DaysPassed / DaysPerWeek + 1;
    public int CurrentMonth => DaysPassed / (DaysPerWeek * WeeksPerMonth) + 1;

    public override void _Ready()
    {
        Hub.Instance.Publish(new NewDay(DaysPassed, CurrentDay, CurrentWeek, CurrentMonth));
    }
    
    public void Handle(DayAdvanced eventData)
    {
        DaysPassed++;
        
        Hub.Instance.Publish(new NewDay(DaysPassed, CurrentDay, CurrentWeek, CurrentMonth));

        if (CurrentDay == 1)
            Hub.Instance.Publish(new NewWeek(DaysPassed, CurrentDay, CurrentWeek, CurrentMonth));

        if (CurrentWeek == 1)
            Hub.Instance.Publish(new NewMonth(DaysPassed, CurrentDay, CurrentWeek, CurrentMonth));
    }
}
