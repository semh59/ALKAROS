using System.Text.RegularExpressions;
using Xunit;

namespace ALKAROS.Host.Experience.PaymentTender.Tests;

/// <summary>Every message the tender, cash and refund endpoints can send to a screen is Turkish.</summary>
public sealed class ErrorMessageLanguageTests
{
    private static readonly string[] Sources =
    [
        "src/Host/DualScreen/DualScreenApplication.Payments.cs",
        "src/Host/DualScreen/DualScreenApplication.CashSession.cs",
        "src/Host/Experience/Billing/BillingSplitApplication.cs",
        "src/Host/Composition/Errors/ApiErrorCatalog.cs",
        "src/Host/Experience/Billing/PaymentRefunds.cs",
        "src/Host/Experience/CustomerAccounts/DualScreenApplication.CustomerAccounts.cs",
    ];

    private static readonly Regex ResponseMessage = new(
        "\"[A-Z][A-Z0-9_]{3,}\",\\s*\\$?\"(?<a>(?:[^\"\\\\]|\\\\.)*)\"|message\\s*=\\s*\\$?\"(?<b>(?:[^\"\\\\]|\\\\.)*)\"",
        RegexOptions.Compiled);

    private static readonly Regex EnglishWord = new(
        "\\b(the|The|is|was|not|must|cannot|Cannot|already|invalid|Invalid|failed|Failed|error|Error|bill|payment|Payment|please|Please|you|for|with|has|have|refused|exceeds|amount|Amount|should|could|unable|Unable|missing|Missing|required|Required|found|insufficient|Insufficient|denied|Denied|unauthorized|Unauthorized|forbidden|Forbidden|unknown|Unknown|exist|exists|allowed|enough|expired|request|Request|does|are|of|to|be|been)\\b",
        RegexOptions.Compiled);

    [Fact]
    public void NoResponseMessageOfTheTenderCashAndRefundEndpointsIsEnglish()
    {
        var messages = Sources
            .SelectMany(source => ResponseMessage.Matches(File.ReadAllText(Path.Combine(FindRoot(), source)))
                .Select(match => (Source: source, Text: match.Groups["a"].Success ? match.Groups["a"].Value : match.Groups["b"].Value)))
            .ToArray();

        Assert.True(messages.Length >= 250, $"Only {messages.Length} messages were found; the scan no longer sees the response shapes.");
        Assert.Empty(messages.Where(message => EnglishWord.IsMatch(message.Text)).Select(message => $"{message.Source}: {message.Text}"));
    }

    private static string FindRoot()
    {
        var directory = AppContext.BaseDirectory;
        while (directory is not null && !File.Exists(Path.Combine(directory, "ALKAROS.slnx")))
            directory = Path.GetDirectoryName(directory);
        return directory ?? throw new InvalidOperationException("Repository root not found.");
    }
}
