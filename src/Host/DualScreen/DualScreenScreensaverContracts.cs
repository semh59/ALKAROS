namespace ALKAROS.Host.DualScreen;

/// <summary>
/// V1-CDP-001: the business's own idle-screen image — one global row, no
/// per-terminal branding (no deployment in this app is multi-branch yet).
/// </summary>
public sealed record ScreensaverImageStatus(bool HasImage, string? ContentType, DateTimeOffset? UpdatedAt);
