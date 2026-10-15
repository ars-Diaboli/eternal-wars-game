namespace EternalWars.Core.Domain.Common;

/// <summary>
/// HoMM-style calendar: 7 days per week, 4 weeks per month. <see cref="AbsoluteDay"/> starts at 1.
/// </summary>
public readonly record struct GameDate(int AbsoluteDay)
{
    public const int DaysPerWeek = 7;
    public const int WeeksPerMonth = 4;

    public static GameDate First { get; } = new(1);

    public int Day => ((AbsoluteDay - 1) % DaysPerWeek) + 1;

    public int Week => (((AbsoluteDay - 1) / DaysPerWeek) % WeeksPerMonth) + 1;

    public int Month => ((AbsoluteDay - 1) / (DaysPerWeek * WeeksPerMonth)) + 1;

    public bool IsFirstDayOfWeek => Day == 1;

    public bool IsFirstDayOfMonth => Day == 1 && Week == 1;

    public GameDate Next() => new(AbsoluteDay + 1);

    public override string ToString() => $"Month {Month}, Week {Week}, Day {Day}";
}
