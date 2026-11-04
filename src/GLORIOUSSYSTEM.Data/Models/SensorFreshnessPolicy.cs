namespace GLORIOUSSYSTEM.Data.Models;

public enum SensorFreshnessState
{
    Missing,
    Fresh,
    Stale
}

public static class SensorFreshnessPolicy
{
    // Sensor telemetry is considered stale after two refresh intervals of the seeded hourly cadence.
    public static readonly TimeSpan StaleAfter = TimeSpan.FromHours(2);

    public static SensorFreshnessState GetState(DateTime? timestamp, DateTime nowUtc)
    {
        if (!timestamp.HasValue)
            return SensorFreshnessState.Missing;

        var timestampUtc = timestamp.Value.Kind == DateTimeKind.Utc
            ? timestamp.Value
            : DateTime.SpecifyKind(timestamp.Value, DateTimeKind.Utc);

        return nowUtc - timestampUtc > StaleAfter
            ? SensorFreshnessState.Stale
            : SensorFreshnessState.Fresh;
    }
}
