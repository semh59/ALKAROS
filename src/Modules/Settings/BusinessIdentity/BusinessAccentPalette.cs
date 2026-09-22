namespace ALKAROS.Settings.BusinessIdentity;

/// <summary>
/// V1-SET-007: a small, pre-vetted set of accent colors a business can pick
/// for its customer-facing QR ordering pages — never a free hex field.
/// CustomerWeb's own CSS (menu-app.css/order-entry.css/bill.css) uses its
/// accent token two ways: as plain text/border color on the page's white
/// background, and as the fill under white button/tab text
/// (--cw-accent-contrast, currently a fixed #ffffff). Every entry here was
/// checked against #FFFFFF for both roles with the standard WCAG 2.1
/// relative-luminance/contrast formula and clears 4.5:1 (AA, normal text)
/// on both — the same bar docs/design/foundations.md §1 holds its own
/// palette to. The two roles compare the identical pair of colors, so one
/// ratio covers both checks.
///
/// The current shipped default, #B5772F, does NOT clear this bar (3.72:1) —
/// deliberately not included as-is. Its darkened, same-hue replacement
/// (#9C6323, 4.97:1) is offered as the "amber" entry instead, but is not
/// this palette's own default (see DefaultKey below).
/// </summary>
public static class BusinessAccentPalette
{
    public sealed record Entry(string Key, string Label, string Hex);

    /// <summary>
    /// V1-RMD-257: an unconfigured business's QR pages default to
    /// "lacivert" (#1B4D7B) — the exact same --color-brand token Cashier,
    /// WaiterPwa and PosTerminal's own design system already use
    /// (docs/design/foundations.md §1). An unset install this way looks
    /// like one coherent product instead of pairing ALKAROS's own navy
    /// staff tools with an unrelated brown/amber customer page.
    /// </summary>
    public const string DefaultKey = "lacivert";

    public static readonly IReadOnlyList<Entry> All =
    [
        new("amber", "Amber", "#9C6323"),
        new("bordo", "Bordo", "#8C2F39"),
        new("yesil", "Koyu Yeşil", "#2F6B3A"),
        new("lacivert", "Lacivert", "#1B4D7B"),
        new("petrol", "Petrol", "#1F6F6B"),
        new("mor", "Mor", "#5B3A6E"),
        new("tugla", "Tuğla", "#B24A1F"),
        new("antrasit", "Antrasit", "#3C4550"),
    ];

    /// <summary>
    /// Never returns an unvetted color: an unknown or missing key (a stale
    /// value from a since-shrunk palette, a manual DB edit) falls back to
    /// <see cref="DefaultKey"/> rather than surfacing whatever was stored.
    /// </summary>
    public static Entry Resolve(string? key)
        => All.FirstOrDefault(entry => entry.Key == key)
           ?? All.First(entry => entry.Key == DefaultKey);
}
