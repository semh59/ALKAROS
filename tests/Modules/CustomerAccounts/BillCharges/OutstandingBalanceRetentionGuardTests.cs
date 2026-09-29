using System.Security.Cryptography;
using ALKAROS.CustomerAccounts.BalanceProjection;
using ALKAROS.CustomerAccounts.BillCharges.Tests.Fixtures;
using ALKAROS.CustomerAccounts.Retention;
using ALKAROS.CustomerAccounts.TransactionLedger;
using ALKAROS.CustomerData.AnonymizationState;
using ALKAROS.CustomerData.Profiles;
using ALKAROS.ModuleComposition;
using ALKAROS.Secrets;
using FluentAssertions;
using Npgsql;
using Xunit;

namespace ALKAROS.CustomerAccounts.BillCharges.Tests;

/// <summary>
/// V1-RMD-435 (V1-RMD-393 F-13): the anonymization retention guard reads the customer's real account balance
/// instead of never blocking.
/// </summary>
public sealed class OutstandingBalanceRetentionGuardTests : IClassFixture<AccountChargeTestDatabase>
{
    private readonly PostgresCustomerProfileStore _profiles;
    private readonly PostgresAccountTransactionLedger _ledger;
    private readonly OutstandingBalanceRetentionGuard _guard;

    public OutstandingBalanceRetentionGuardTests(AccountChargeTestDatabase database)
    {
        NpgsqlDataSource dataSource = database.DataSource;
        var secretProvider = new InMemorySecretProvider();
        secretProvider.Set(new SecretReference("envelope-master-key"), Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
        _profiles = new PostgresCustomerProfileStore(dataSource, secretProvider);
        _ledger = new PostgresAccountTransactionLedger(dataSource);
        _guard = new OutstandingBalanceRetentionGuard(new PostgresAccountBalanceProjection(dataSource, _ledger));
    }

    [Fact]
    public async Task ACustomerWhoStillOwesTheVenueIsBlocked()
    {
        var customerId = await SeedCustomerAsync();
        await RecordAsync(customerId, AccountTransactionType.Charge, 150m);

        var reason = await _guard.CheckAsync(customerId, CancellationToken.None);

        reason.Should().Be("Müşterinin kapanmamış cari borcu var (150,00 TL); borç kapanmadan anonimleştirilemez.");
    }

    [Fact]
    public async Task ACustomerTheVenueOwesMoneyToIsBlocked()
    {
        var customerId = await SeedCustomerAsync();
        await RecordAsync(customerId, AccountTransactionType.Credit, 40m);

        var reason = await _guard.CheckAsync(customerId, CancellationToken.None);

        reason.Should().Be("Müşterinin cari hesabında alacağı var (40,00 TL); iade edilmeden anonimleştirilemez.");
    }

    [Fact]
    public async Task ACustomerWhoseBalanceIsSettledIsNotBlocked()
    {
        var customerId = await SeedCustomerAsync();
        await RecordAsync(customerId, AccountTransactionType.Charge, 150m);
        await RecordAsync(customerId, AccountTransactionType.Payment, 150m);

        (await _guard.CheckAsync(customerId, CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task ACustomerWithNoAccountActivityIsNotBlocked()
    {
        var customerId = await SeedCustomerAsync();

        (await _guard.CheckAsync(customerId, CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public void TheProductionModuleCatalogRegistersTheBalanceGuard()
    {
        var context = new ModuleContext();
        new CustomerAccountsBillChargesModule().Register(context);

        context.Services
            .Where(service => service.ServiceType == typeof(IAnonymizationRetentionGuard))
            .Should().ContainSingle()
            .Which.ImplementationType.Should().Be<OutstandingBalanceRetentionGuard>();
    }

    private async Task<Guid> SeedCustomerAsync() =>
        await _profiles.CreateAsync(new CreateCustomerProfileRequest("Test Customer", null, null, null));

    private async Task RecordAsync(Guid customerId, AccountTransactionType type, decimal amount) =>
        await _ledger.RecordAsync(new RecordAccountTransactionRequest(
            customerId,
            type,
            amount,
            sourceReferenceType: "Test",
            sourceReferenceId: Guid.NewGuid(),
            note: null,
            createdBy: null,
            occurredAt: DateTimeOffset.UtcNow));
}
