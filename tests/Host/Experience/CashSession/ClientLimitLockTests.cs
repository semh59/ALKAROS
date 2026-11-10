using System.Globalization;
using System.Text.RegularExpressions;
using ALKAROS.Cash.Contracts;
using ALKAROS.Cash.TransactionLedger;
using ALKAROS.Host.Experience.Billing;
using ALKAROS.Settings.BillAdjustmentCap;
using ALKAROS.Settings.CashCloseTolerance;
using ALKAROS.Settings.CashMovementApproval;
using ALKAROS.Settings.KitchenDenseModeThreshold;
using ALKAROS.Settings.QrOrderExpiry;
using ALKAROS.Settings.TypedSettings;
using ALKAROS.Settings.WaiterMaxActiveTables;
using Xunit;

namespace ALKAROS.Host.Experience.CashSession.Tests;

/// <summary>
/// The limits a screen shows or enforces before sending are copies of server rules. These tests fail when either
/// side moves alone, so the screen can never accept what the server refuses or refuse what it accepts.
/// </summary>
public sealed class ClientLimitLockTests
{
    private static readonly string Root = FindRoot();
    private static readonly Regex DictionaryEntry = new("^\\s+[A-Z][A-Z0-9_]+: '", RegexOptions.Compiled);

    public static TheoryData<string, string, long, long> SettingRanges()
    {
        var models = File.ReadAllText(Path.Combine(
            Root, "src", "Clients", "PosTerminal", "src", "features", "management-settings-history", "models.ts"));
        var rules = Regex.Matches(
            models, "\"(?<key>[a-z_.]+)\": \\{ kind: \"(?<kind>number|minutes)\", min: (?<min>\\d+), max: (?<max>\\d+),");
        Assert.Equal(
            new[]
            {
                WaiterMaxActiveTablesSetting.Key, KitchenDenseModeThresholdSetting.Key, QrOrderExpirySetting.Key,
                CashOutApprovalThresholdSetting.Key, CloseVarianceToleranceSetting.Key, BillAdjustmentTotalCapSetting.Key,
            }.Order(),
            rules.Select(rule => rule.Groups["key"].Value).Order());
        var data = new TheoryData<string, string, long, long>();
        foreach (Match rule in rules)
            data.Add(rule.Groups["key"].Value, rule.Groups["kind"].Value, long.Parse(rule.Groups["min"].Value, CultureInfo.InvariantCulture), long.Parse(rule.Groups["max"].Value, CultureInfo.InvariantCulture));
        return data;
    }

    [Theory]
    [MemberData(nameof(SettingRanges))]
    public void TheManagementScreenRangeOfASettingIsExactlyTheRangeTheServerAccepts(string key, string kind, long min, long max)
    {
        var validator = new SettingValidator();
        var type = kind == "minutes" ? SettingDataType.Duration : SettingDataType.WholeNumber;
        string Text(long value) => kind == "minutes"
            ? TimeSpan.FromMinutes(value).ToString("c", CultureInfo.InvariantCulture)
            : value.ToString(CultureInfo.InvariantCulture);

        validator.ValidateValue(key, Text(min), type);
        validator.ValidateValue(key, Text(max), type);
        if (min > 0)
            Assert.ThrowsAny<SettingsException>(() => validator.ValidateValue(key, Text(min - 1), type));
        else
            Assert.ThrowsAny<SettingsException>(() => validator.ValidateValue(key, Text(-1), type));
        Assert.ThrowsAny<SettingsException>(() => validator.ValidateValue(key, Text(max + 1), type));
    }

    [Fact]
    public void TheCashScreenNoteLimitIsTheServerNoteLimit()
    {
        Assert.Equal(CashSessionPolicy.MaxExplanationLength, CashTransaction.MaxReasonLength);
        var script = File.ReadAllText(Path.Combine(
            Root, "src", "Clients", "Cashier", "wwwroot", "payments", "cash-session", "cash-session.js"));
        var limits = Limits(script);
        Assert.Equal(7, limits.Length);
        Assert.All(limits, limit => Assert.Equal(CashSessionPolicy.MaxExplanationLength, limit));
    }

    // Every client file that calls the refund endpoints, found rather than listed, so a new refund screen is covered at once.
    public static TheoryData<string> RefundScreens()
    {
        var clients = Path.Combine(FindRoot(), "src", "Clients");
        var data = new TheoryData<string>();
        var found = 0;
        foreach (var file in Directory.EnumerateFiles(clients, "*.*", SearchOption.AllDirectories)
                     .Where(path => path.EndsWith(".js", StringComparison.Ordinal) || path.EndsWith(".ts", StringComparison.Ordinal)
                         || path.EndsWith(".tsx", StringComparison.Ordinal))
                     .Where(path => !path.Contains("node_modules", StringComparison.Ordinal)
                         && !path.Contains(Path.DirectorySeparatorChar + "dist" + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                         && !path.Contains(".test.", StringComparison.Ordinal)))
        {
            if (File.ReadAllText(file).Contains("refund", StringComparison.OrdinalIgnoreCase)
                && Regex.IsMatch(File.ReadAllText(file), "max[Ll]ength"))
            {
                found++;
                data.Add(Path.GetRelativePath(FindRoot(), file).Replace(Path.DirectorySeparatorChar, '/'));
            }
        }

        Assert.True(found >= 2, "The refund screens could not be found.");
        return data;
    }

    [Theory]
    [MemberData(nameof(RefundScreens))]
    public void TheRefundScreensTextAndSlipLimitsAreTheServerLimits(string screen)
    {
        // The card-charged claim slip of the payment screen has its own rule, locked next to the endpoint that enforces it.
        // An error dictionary entry is a server sentence copied word for word (error-dictionary-lock.test.js compares it), so its numbers are the server's own.
        var source = string.Join(Environment.NewLine, File.ReadAllLines(Path.Combine(Root, screen.Replace('/', Path.DirectorySeparatorChar)))
            .Where(line => !line.Contains("claim-slip", StringComparison.Ordinal) && !DictionaryEntry.IsMatch(line)));
        var limits = Limits(source);

        Assert.Contains(PaymentRefundStore.MaxTextLength, limits);
        Assert.Contains(PaymentRefundStore.MaxSlipLength, limits);
        Assert.All(limits, limit => Assert.True(
            limit == PaymentRefundStore.MaxTextLength || limit == PaymentRefundStore.MaxSlipLength,
            $"{screen} states a length limit of {limit} that no server rule has."));
    }

    // Every number a screen ties to a length: an input attribute, a script check or a sentence for the user.
    private static int[] Limits(string source) => Regex
        .Matches(source, "max[Ll]ength[=:]?\\{?\"?(\\d+)|(?:notes|note|reason)\\.length > (\\d+)|(\\d+) karakter")
        .Select(match => int.Parse(match.Groups.Cast<Group>().Skip(1).First(group => group.Success).Value, CultureInfo.InvariantCulture))
        .ToArray();

    private static string FindRoot()
    {
        var directory = AppContext.BaseDirectory;
        while (directory is not null && !File.Exists(Path.Combine(directory, "ALKAROS.slnx")))
            directory = Path.GetDirectoryName(directory);
        return directory ?? throw new InvalidOperationException("Repository root not found.");
    }
}
