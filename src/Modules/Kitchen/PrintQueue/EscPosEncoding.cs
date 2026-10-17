namespace ALKAROS.Kitchen.PrintQueue;

using System.Text;

/// <summary>The printer family whose Turkish code table command is used; Ascii folds Turkish letters for printers without one.</summary>
public enum PrinterProfile
{
    Epson,
    Bixolon,
    Star,
    Ascii,
}

/// <summary>
/// Turns ticket text into the bytes of the printer's own Turkish code page (857). The table is selected with the
/// command of the printer family: Epson and Bixolon use ESC t (n = 13 and 37), Star uses ESC GS t 12 in ESC/POS mode.
/// </summary>
public static class EscPosEncoding
{
    public const string ProfileVariable = "ALKAROS_PRINTER_PROFILE";

    private const string TurkishLetters = "ÇçĞğİıÖöŞşÜü";
    private const string AsciiLetters = "CcGgIiOoSsUu";

    private static readonly Encoding CodePage857 = CodePagesEncodingProvider.Instance.GetEncoding(
        857, new EncoderReplacementFallback("?"), DecoderFallback.ReplacementFallback)!;
    private static readonly Encoding AsciiFallback = Encoding.GetEncoding(
        "us-ascii", new EncoderReplacementFallback("?"), DecoderFallback.ReplacementFallback);

    public static PrinterProfile FromEnvironment() =>
        Enum.TryParse<PrinterProfile>(Environment.GetEnvironmentVariable(ProfileVariable), ignoreCase: true, out var profile)
            ? profile
            : PrinterProfile.Epson;

    public static byte[] SelectTable(PrinterProfile profile) => profile switch
    {
        PrinterProfile.Epson => [0x1B, 0x74, 13],
        PrinterProfile.Bixolon => [0x1B, 0x74, 37],
        PrinterProfile.Star => [0x1B, 0x1D, 0x74, 12],
        _ => [],
    };

    public static byte[] Encode(string text, PrinterProfile profile)
    {
        ArgumentNullException.ThrowIfNull(text);
        return profile == PrinterProfile.Ascii ? AsciiFallback.GetBytes(Fold(text)) : CodePage857.GetBytes(text);
    }

    private static string Fold(string text)
    {
        var folded = new StringBuilder(text.Length);
        foreach (var letter in text)
        {
            var index = TurkishLetters.IndexOf(letter, StringComparison.Ordinal);
            folded.Append(index >= 0 ? AsciiLetters[index] : letter);
        }

        return folded.ToString();
    }
}
