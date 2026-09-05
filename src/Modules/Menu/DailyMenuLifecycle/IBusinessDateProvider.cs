namespace ALKAROS.Menu.DailyMenuLifecycle;

public interface IBusinessDateProvider
{
    DateOnly GetCurrentBusinessDate(DateTimeOffset? utcNow = null);
}

public sealed class BusinessDateProvider : IBusinessDateProvider
{
    private readonly TimeZoneInfo _businessTimeZone;
    private readonly TimeSpan _cutoffTime;

    public BusinessDateProvider(TimeZoneInfo? timeZone = null, TimeSpan? cutoffTime = null)
    {
        _businessTimeZone = timeZone ?? ResolveDefaultTimeZone();
        _cutoffTime = cutoffTime ?? new TimeSpan(23, 59, 59);
    }

    public DateOnly GetCurrentBusinessDate(DateTimeOffset? utcNow = null)
    {
        var utc = utcNow ?? DateTimeOffset.UtcNow;
        var localTime = TimeZoneInfo.ConvertTime(utc, _businessTimeZone);

        if (_cutoffTime < new TimeSpan(23, 59, 59) && localTime.TimeOfDay < _cutoffTime)
        {
            return DateOnly.FromDateTime(localTime.DateTime.AddDays(-1));
        }

        return DateOnly.FromDateTime(localTime.DateTime);
    }

    private static TimeZoneInfo ResolveDefaultTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");
        }
        catch (TimeZoneNotFoundException)
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Turkey Standard Time");
            }
            catch
            {
                return TimeZoneInfo.Local;
            }
        }
    }
}
