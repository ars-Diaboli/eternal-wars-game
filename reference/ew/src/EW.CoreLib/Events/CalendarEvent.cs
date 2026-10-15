namespace EW.CoreLib.Types.Events;

public record CalendarEvent(int DaysPassed, int CurrentDay, int CurrentWeek, int CurrentMonth) : IGameEvent;
public record NewDay(int DaysPassed, int CurrentDay, int CurrentWeek, int CurrentMonth) 
    : CalendarEvent(DaysPassed, CurrentDay, CurrentWeek, CurrentMonth);
public record NewWeek(int DaysPassed, int CurrentDay, int CurrentWeek, int CurrentMonth) 
    : CalendarEvent(DaysPassed, CurrentDay, CurrentWeek, CurrentMonth);
public record NewMonth(int DaysPassed, int CurrentDay, int CurrentWeek, int CurrentMonth) 
    : CalendarEvent(DaysPassed, CurrentDay, CurrentWeek, CurrentMonth);
public sealed class DayAdvanced : IGameEvent;