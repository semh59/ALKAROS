using System.Net;
using System.Text.RegularExpressions;
using Xunit;

namespace ALKAROS.Host.Experience.PaymentTender.Tests;

public sealed partial class PaymentTenderHttpTests
{
    [Fact]
    public async Task TheCardClaimSlipLengthOnThePaymentScreenIsTheLongestSlipTheServerAccepts()
    {
        var script = await File.ReadAllTextAsync(Path.Combine(
            RepositoryRoot(), "src", "Clients", "Cashier", "wwwroot", "payments", "split-payment", "split-payment.js"));
        var screenLimit = int.Parse(
            Regex.Match(script, "id=\"claim-slip\" maxlength=\"(\\d+)\"").Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        var terminalId = Guid.NewGuid();
        var cashier = await _database.SeedCashierSessionAsync(terminalId, "rmd794-slip-cashier");
        var manager = await _database.SeedManagerSessionAsync(terminalId, "rmd794-slip-manager");
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var longest = await ClaimWithSlipAsync(client, terminalId, cashier, manager, "slip-fits", new string('A', screenLimit));
        var tooLong = await ClaimWithSlipAsync(client, terminalId, cashier, manager, "slip-over", new string('A', screenLimit + 1));

        Assert.Equal(HttpStatusCode.OK, longest);
        Assert.Equal(HttpStatusCode.BadRequest, tooLong);
    }

    private async Task<HttpStatusCode> ClaimWithSlipAsync(
        HttpClient client, Guid terminalId, string cashier, string manager, string key, string slip)
    {
        var billId = await _database.SeedBillAsync(payable: 40m);
        var paymentId = await CreateUnsettledPaymentAsync(client, terminalId, billId, cashier, key);
        return (await PostAsync(client, ClaimPath(terminalId, billId, paymentId), manager, new { SlipNumber = slip })).StatusCode;
    }

    private static string RepositoryRoot()
    {
        var directory = AppContext.BaseDirectory;
        while (directory is not null && !File.Exists(Path.Combine(directory, "ALKAROS.slnx")))
            directory = Path.GetDirectoryName(directory);
        return directory ?? throw new InvalidOperationException("Repository root not found.");
    }
}
