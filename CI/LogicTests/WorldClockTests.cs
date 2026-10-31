using System;

public static class WorldClockTests
{
    public static void Register(TestRunner run)
    {
        run.Test("Clock_DefaultStartsOnDayOneAtNine", () => {
            var clock = new WorldClock();
            Near(9f, clock.Hour);
            Assert.Equal(1, clock.Day);
        });
        run.Test("Clock_OneEighthOfDayAdvancesThreeHours", () => {
            var clock = new WorldClock(9f);
            clock.Advance(60f, 480f);
            Near(12f, clock.Hour);
        });
        run.Test("Clock_MidnightWrapsAndIncrementsDay", () => {
            var clock = new WorldClock(23.5f);
            clock.Advance(20f, 480f);
            Near(0.5f, clock.Hour);
            Assert.Equal(2, clock.Day);
        });
        run.Test("Clock_LongFrameCountsEveryPassedDay", () => {
            var clock = new WorldClock(9f);
            clock.Advance(1200f, 480f);
            Near(21f, clock.Hour);
            Assert.Equal(3, clock.Day);
        });
        run.Test("Clock_TimeScaleZeroDoesNotAdvance", () => {
            var clock = new WorldClock(9f);
            clock.Advance(0f, 480f);
            Near(9f, clock.Hour);
        });
        run.Test("Clock_InvalidTimingsCannotPoisonState", () => {
            var clock = new WorldClock(9f);
            foreach (float bad in new[] { -1f, 0f, float.NaN, float.PositiveInfinity })
            {
                clock.Advance(bad, 480f);
                clock.Advance(1f, bad);
            }
            clock.SetHour(float.NaN);
            Near(9f, clock.Hour);
            Assert.Equal(1, clock.Day);
        });
        run.Test("Clock_PreviewHoursWrapWithoutAddingDays", () => {
            var clock = new WorldClock();
            clock.SetHour(-1f);
            Near(23f, clock.Hour);
            clock.SetHour(24f);
            Near(0f, clock.Hour);
            Assert.Equal(1, clock.Day);
        });
    }

    private static void Near(float expected, float actual)
    {
        Assert.True(Math.Abs(expected - actual) < 0.0001f, $"expected {expected}, got {actual}");
    }
}
