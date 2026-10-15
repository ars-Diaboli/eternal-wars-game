using EW.CoreLib.Messaging;
using EW.CoreLib.Types.Events;

namespace EW.CoreLib;

public sealed class GameCalendar : IHandleEvent<DayAdvanced>
{
    private const int DaysPerWeek = 7;
    private const int WeeksPerMonth = 4;

    public int DaysPassed { get; private set; } = 1;
    public int CurrentDay => DaysPassed % DaysPerWeek;
    public int CurrentWeek => DaysPassed / DaysPerWeek + 1;
    public int CurrentMonth => DaysPassed / (DaysPerWeek * WeeksPerMonth) + 1;

    // public override void _Ready()
    // {
    //     Hub.Instance.Publish(new NewDay(DaysPassed, CurrentDay, CurrentWeek, CurrentMonth));
    // }

    public void Handle(DayAdvanced e)
    {
        DaysPassed++;

        Hub.Instance.Publish(new NewDay(DaysPassed, CurrentDay, CurrentWeek, CurrentMonth));

        if (CurrentDay == 1)
            Hub.Instance.Publish(new NewWeek(DaysPassed, CurrentDay, CurrentWeek, CurrentMonth));

        if (CurrentWeek == 1)
            Hub.Instance.Publish(new NewMonth(DaysPassed, CurrentDay, CurrentWeek, CurrentMonth));
    }
}