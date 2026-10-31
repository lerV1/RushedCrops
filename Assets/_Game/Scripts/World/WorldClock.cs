using System;

// The clock has no Unity dependency, so CI can check midnight and timing rules.
public sealed class WorldClock
{
    private double hour;

    public float Hour => (float)hour;
    public int Day { get; private set; } = 1;

    public WorldClock(float startingHour = 9f)
    {
        SetHour(startingHour);
    }

    public void SetHour(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value)) return;
        hour = ((value % 24d) + 24d) % 24d;
    }

    public void Advance(float elapsedSeconds, float dayLengthSeconds)
    {
        if (!IsPositiveFinite(elapsedSeconds) || !IsPositiveFinite(dayLengthSeconds)) return;

        double totalHours = hour + (double)elapsedSeconds / dayLengthSeconds * 24d;
        double passedDays = Math.Floor(totalHours / 24d);
        Day = (int)Math.Min(int.MaxValue, Day + passedDays);
        hour = totalHours % 24d;
    }

    private static bool IsPositiveFinite(float value)
    {
        return value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
