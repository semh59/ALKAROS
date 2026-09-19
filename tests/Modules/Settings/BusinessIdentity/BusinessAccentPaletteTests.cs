using FluentAssertions;
using Xunit;

namespace ALKAROS.Settings.BusinessIdentity.Tests;

/// <summary>
/// V1-SET-007: the whole point of a curated palette instead of a free hex
/// field is that every entry is actually safe to use. This independently
/// recomputes the standard WCAG 2.1 relative-luminance/contrast formula
/// (not a copy of any production code path) against #FFFFFF for every
/// palette entry — if a future edit ever adds an unvetted color, this
/// fails instead of silently shipping it.
/// </summary>
public sealed class BusinessAccentPaletteTests
{
    private const double MinimumContrast = 4.5; // WCAG 2.1 AA, normal text.

    [Theory]
    [MemberData(nameof(PaletteEntries))]
    public void EveryPaletteColorClearsWcagAaAgainstWhiteBothWaysItIsUsed(BusinessAccentPalette.Entry entry)
    {
        var ratio = ContrastRatio(entry.Hex, "#FFFFFF");

        // CustomerWeb's own CSS uses the accent both as plain text/border on
        // a white page (foreground) and as the fill under white button/tab
        // text (background) — both compare the identical pair of colors, so
        // one ratio covers both roles.
        ratio.Should().BeGreaterOrEqualTo(MinimumContrast,
            $"{entry.Key} ({entry.Hex}) must be readable both as text on white and under white text");
    }

    [Fact]
    public void TheDefaultKeyIsAnActualPaletteEntry()
        => BusinessAccentPalette.All.Should().Contain(entry => entry.Key == BusinessAccentPalette.DefaultKey);

    [Fact]
    public void EveryKeyIsUnique()
        => BusinessAccentPalette.All.Select(entry => entry.Key).Should().OnlyHaveUniqueItems();

    [Fact]
    public void TheCurrentShippedCustomerWebDefaultDoesNotClearTheBarOnItsOwn()
    {
        // #B5772F is CustomerWeb's own hardcoded --cw-accent
        // (menu-app.css/order-entry.css/bill.css) today. Documenting, not
        // asserting a requirement: this is exactly why it was not included
        // in the palette as-is (see BusinessAccentPalette's own remarks).
        ContrastRatio("#B5772F", "#FFFFFF").Should().BeLessThan(MinimumContrast);
    }

    public static TheoryData<BusinessAccentPalette.Entry> PaletteEntries()
    {
        var data = new TheoryData<BusinessAccentPalette.Entry>();
        foreach (var entry in BusinessAccentPalette.All) data.Add(entry);
        return data;
    }

    private static double ContrastRatio(string hexA, string hexB)
    {
        var luminanceA = RelativeLuminance(hexA);
        var luminanceB = RelativeLuminance(hexB);
        var lighter = Math.Max(luminanceA, luminanceB);
        var darker = Math.Min(luminanceA, luminanceB);
        return (lighter + 0.05) / (darker + 0.05);
    }

    private static double RelativeLuminance(string hex)
    {
        hex = hex.TrimStart('#');
        var r = LinearizeChannel(Convert.ToInt32(hex[..2], 16));
        var g = LinearizeChannel(Convert.ToInt32(hex[2..4], 16));
        var b = LinearizeChannel(Convert.ToInt32(hex[4..6], 16));
        return 0.2126 * r + 0.7152 * g + 0.0722 * b;
    }

    private static double LinearizeChannel(int channel)
    {
        var c = channel / 255.0;
        return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
    }
}
