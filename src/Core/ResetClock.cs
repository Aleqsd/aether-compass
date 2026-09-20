namespace AetherCompass.Core;

/// <summary>Game resets use UTC and never follow local daylight-saving transitions.</summary>
public static class ResetClock
{
    public static DateTimeOffset PeriodStart(DateTimeOffset at, ResetCadence cadence)
    {
        var utc = at.ToUniversalTime();
        if (cadence == ResetCadence.None) return DateTimeOffset.MinValue;
        var hour = cadence == ResetCadence.Weekly ? 8 : 15;
        var candidate = new DateTimeOffset(utc.Year, utc.Month, utc.Day, hour, 0, 0, TimeSpan.Zero);
        if (cadence == ResetCadence.Weekly)
        {
            var elapsedDays = ((int)utc.DayOfWeek - (int)DayOfWeek.Tuesday + 7) % 7;
            candidate = candidate.AddDays(-elapsedDays);
            if (candidate > utc) candidate = candidate.AddDays(-7);
        }
        else if (candidate > utc) candidate = candidate.AddDays(-1);
        return candidate;
    }

    public static DateTimeOffset? NextReset(DateTimeOffset at, ResetCadence cadence)
        => cadence == ResetCadence.None ? null : PeriodStart(at, cadence).AddDays(cadence == ResetCadence.Weekly ? 7 : 1);

    public static bool IsCurrent(DateTimeOffset observation, DateTimeOffset now, ResetCadence cadence)
        => observation <= now && observation >= PeriodStart(now, cadence);
}
