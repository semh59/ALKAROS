namespace ALKAROS.Menu.DailyMenuLifecycle;

public sealed record DailyMenuItemHistory(
    Guid Id,
    Guid DailyMenuItemId,
    string OldValueJson,
    string NewValueJson,
    Guid? ChangedBy,
    DateTimeOffset ChangedAt);
