using ALKAROS.Invoicing.Generation.OrderInvoices;
using ALKAROS.Invoicing.Generation.Tests.Fixtures;
using Xunit;

namespace ALKAROS.Invoicing.Generation.Tests;

/// <summary>The seller profile: what makes it complete, and that the store keeps exactly one, replaced on save.</summary>
public sealed class SellerProfileTests : IAsyncLifetime
{
    private readonly InvoiceGenerationTestDatabase _database = new();
    private PostgresSellerProfileStore _store = null!;

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();
        _store = new PostgresSellerProfileStore(_database.DataSource);
    }

    public Task DisposeAsync() => _database.DisposeAsync();

    private static SellerProfile Complete() => new(
        "Deniz Lokantası Ltd. Şti.", SellerProfile.Vkn, "1234567890", "Kadıköy", "Moda Cad. 1", "Kadıköy", "İstanbul", "muhasebe@deniz.example");

    [Fact]
    public void ACompleteProfileHasNoProblems()
    {
        Assert.Empty(Complete().Problems());
        Assert.Empty((Complete() with { TaxIdKind = SellerProfile.Tckn, TaxIdNumber = "12345678901", Email = null }).Problems());
    }

    [Theory]
    [InlineData("LegalName", "")]
    [InlineData("TaxOffice", "  ")]
    [InlineData("Address", "")]
    [InlineData("District", "")]
    [InlineData("City", "")]
    [InlineData("TaxIdNumber", "123456789")]
    [InlineData("TaxIdNumber", "12345678901")]
    [InlineData("TaxIdNumber", "12345ABCDE")]
    [InlineData("Email", "not-an-address")]
    public void AnIncompleteFieldIsNamed(string field, string value)
    {
        var profile = field switch
        {
            "LegalName" => Complete() with { LegalName = value },
            "TaxOffice" => Complete() with { TaxOffice = value },
            "Address" => Complete() with { Address = value },
            "District" => Complete() with { District = value },
            "City" => Complete() with { City = value },
            "TaxIdNumber" => Complete() with { TaxIdNumber = value },
            _ => Complete() with { Email = value },
        };

        Assert.Equal([field], profile.Problems());
    }

    [Fact]
    public void AnUnknownTaxIdKindIsAProblemWithTheNumber()
        => Assert.Equal(["TaxIdNumber"], (Complete() with { TaxIdKind = "Passport" }).Problems());

    [Fact]
    public async Task TheStoreHasNoProfileUntilOneIsSavedAndKeepsOnlyTheLatest()
    {
        Assert.Null(await _store.GetAsync());

        await _store.SaveAsync(Complete(), Guid.NewGuid());
        await _store.SaveAsync(Complete() with { LegalName = "Yeni Ünvan A.Ş.", Email = null }, null);

        var saved = await _store.GetAsync();
        Assert.Equal("Yeni Ünvan A.Ş.", saved!.LegalName);
        Assert.Null(saved.Email);
        Assert.Equal(1L, await ScalarAsync("SELECT count(*) FROM invoicing.seller_profile;"));
    }

    [Fact]
    public async Task AnIncompleteProfileIsRefusedAndNothingIsStored()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _store.SaveAsync(Complete() with { TaxIdNumber = "1" }, null));

        Assert.Null(await _store.GetAsync());
    }

    private async Task<long> ScalarAsync(string sql)
    {
        await using var command = _database.DataSource.CreateCommand(sql);
        return (long)(await command.ExecuteScalarAsync())!;
    }
}
