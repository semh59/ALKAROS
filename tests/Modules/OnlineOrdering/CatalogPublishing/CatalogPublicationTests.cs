using ALKAROS.Catalog.ProductCatalog;
using ALKAROS.OnlineOrdering.CatalogPublishing.Yemeksepeti;
using ALKAROS.OnlineOrdering.Yemeksepeti.ProductMapping;
using ALKAROS.OnlineOrdering.Yemeksepeti.StatusSync;
using ALKAROS.TestHelpers;
using FluentAssertions;
using Xunit;

namespace ALKAROS.OnlineOrdering.CatalogPublishing.Tests;

public sealed class CatalogPublishingTestDatabase : PgTestDatabase
{
    public CatalogPublishingTestDatabase() : base("alkaros_catalog_pub_") { }

    protected override async Task ApplySqlAsync()
    {
        foreach (var file in new[]
                 {
                     "002-inbox-messages.up.sql", "003-outbox-messages.up.sql", "006-catalog.up.sql",
                     "033-message-lease-generation.up.sql", "040-wave9-schema-additions.up.sql",
                     "053-catalog-products-row-version.up.sql", "066-static-menu.up.sql", "103-products-prep-time.up.sql",
                     "144-yemeksepeti-product-mappings.up.sql", "147-online-catalog-publications.up.sql"
                 })
        {
            await RunFixtureAsync(file);
        }
    }

    public async Task RunFixtureAsync(string file) =>
        await RunAsync(DataSource, await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", file)));

    public PostgresYemeksepetiProductMappingService Mappings => new(
        DataSource,
        new PostgresProductRepository(DataSource),
        new PostgresProductModifierGroupRepository(DataSource),
        new PostgresModifierGroupRepository(DataSource));

    public async Task<Guid> SeedMenuAsync()
    {
        var menuId = Guid.NewGuid();
        await ExecAsync("INSERT INTO menu.menus (menu_id, code, name) VALUES ($1, $2, 'Online Menü');", menuId, "M-" + menuId.ToString("N")[..8]);
        return menuId;
    }

    public async Task<(Guid ProductId, string Sku)> SeedMenuProductAsync(Guid menuId, decimal? price = 120m, bool active = true, string name = "Adana Dürüm")
    {
        var productId = Guid.NewGuid();
        var sku = "SKU-" + productId.ToString("N")[..8];
        await ExecAsync(
            "INSERT INTO catalog.products (product_id, sku, name, product_type, stock_mode, active, current_price) VALUES ($1, $2, $3, 1, 1, $4, $5);",
            productId, sku, name, active, (object?)price ?? DBNull.Value);
        await ExecAsync(
            "INSERT INTO menu.menu_items (menu_item_id, menu_id, product_id) VALUES ($1, $2, $3);", Guid.NewGuid(), menuId, productId);
        return (productId, sku);
    }

    public Task SetPriceAsync(Guid productId, decimal price) =>
        ExecAsync("UPDATE catalog.products SET current_price = $2 WHERE product_id = $1;", productId, price);

    public Task SetActiveAsync(Guid productId, bool active) =>
        ExecAsync("UPDATE catalog.products SET active = $2 WHERE product_id = $1;", productId, active);

    public async Task AttachModifierGroupAsync(Guid productId)
    {
        var groupId = Guid.NewGuid();
        await ExecAsync(
            "INSERT INTO catalog.modifier_groups (modifier_group_id, code, name, selection_type, min_selections, max_selections) VALUES ($1, $2, 'Ekstralar', 2, 0, 3);",
            groupId, "MG-" + groupId.ToString("N")[..8]);
        await ExecAsync(
            "INSERT INTO catalog.product_modifier_groups (product_modifier_group_id, product_id, modifier_group_id) VALUES ($1, $2, $3);",
            Guid.NewGuid(), productId, groupId);
    }

    public async Task<long> ScalarAsync(string sql, params object[] parameters)
    {
        await using var command = DataSource.CreateCommand(sql);
        foreach (var parameter in parameters)
            command.Parameters.AddWithValue(parameter);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    public async Task<(string Status, int Attempts, string? LastError, string? JobId)> PublicationAsync(Guid publicationId)
    {
        await using var command = DataSource.CreateCommand(
            "SELECT status, delivery_attempts, last_error, provider_job_id FROM online_ordering.catalog_publications WHERE publication_id = $1;");
        command.Parameters.AddWithValue(publicationId);
        await using var reader = await command.ExecuteReaderAsync();
        await reader.ReadAsync();
        return (reader.GetString(0), reader.GetInt32(1), reader.IsDBNull(2) ? null : reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetString(3));
    }

    private async Task ExecAsync(string sql, params object[] parameters)
    {
        await using var command = DataSource.CreateCommand(sql);
        foreach (var parameter in parameters)
            command.Parameters.AddWithValue(parameter);
        await command.ExecuteNonQueryAsync();
    }
}

/// <summary>A test double of the provider call only; everything on our side runs for real.</summary>
public sealed class RecordingPartnerClient : IYemeksepetiPartnerClient
{
    public List<IReadOnlyList<YemeksepetiCatalogProductUpdate>> CatalogUpdates { get; } = [];

    public bool Fail { get; set; }

    public Task UpdateOrderStatusAsync(YemeksepetiStatusUpdateRequested update, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Catalog publishing never sends order status updates.");

    public Task<string?> UpdateVendorCatalogAsync(IReadOnlyList<YemeksepetiCatalogProductUpdate> products, CancellationToken cancellationToken = default)
    {
        if (Fail)
            throw new YemeksepetiPartnerApiException("Yemeksepeti catalog update failed with HTTP 503.");
        CatalogUpdates.Add(products);
        return Task.FromResult<string?>("job-" + CatalogUpdates.Count);
    }
}

public sealed class CatalogPublicationTests : IClassFixture<CatalogPublishingTestDatabase>
{
    private readonly CatalogPublishingTestDatabase _db;
    private readonly RecordingPartnerClient _provider = new();
    private readonly CatalogPublicationService _service;
    private readonly Guid _manager = Guid.NewGuid();

    public CatalogPublicationTests(CatalogPublishingTestDatabase db)
    {
        _db = db;
        _service = new CatalogPublicationService(
            db.DataSource, [new YemeksepetiCatalogPublisher(db.Mappings, _provider, TimeProvider.System)]);
    }

    private Task<CatalogPublicationSummary> PublishAsync(Guid menuId) =>
        _service.RequestAsync(YemeksepetiCatalogPublisher.ChannelName, menuId, _manager);

    private Task<long> OutboxCountAsync(Guid publicationId) =>
        _db.ScalarAsync("SELECT count(*) FROM outbox_messages WHERE aggregate_id = $1;", publicationId);

    [Fact]
    public async Task AMenuIsPublishedUnderStableSkusAndDeliveredThroughTheOutbox()
    {
        var menu = await _db.SeedMenuAsync();
        var (doner, donerSku) = await _db.SeedMenuProductAsync(menu, 120m);
        var (_, ayranSku) = await _db.SeedMenuProductAsync(menu, 25.5m, name: "Ayran");

        var summary = await PublishAsync(menu);

        summary.Status.Should().Be(CatalogPublicationStatus.Pending);
        summary.ItemCount.Should().Be(2);
        summary.ValidationErrors.Should().BeEmpty();
        (await OutboxCountAsync(summary.PublicationId)).Should().Be(1);
        (await _db.Mappings.FindOpenSkuForProductAsync(doner)).Should().Be(donerSku);

        await _service.DeliverAsync(summary.PublicationId);

        _provider.CatalogUpdates.Should().ContainSingle().Which.Should().BeEquivalentTo(new[]
        {
            new YemeksepetiCatalogProductUpdate(donerSku, 120m, true, null),
            new YemeksepetiCatalogProductUpdate(ayranSku, 25.5m, true, null)
        });
        (await _db.PublicationAsync(summary.PublicationId)).Should().Be(("Delivered", 1, (string?)null, (string?)"job-1"));
    }

    [Fact]
    public async Task RepublishingTheSameMenuNeverCreatesASecondExternalProduct()
    {
        var menu = await _db.SeedMenuAsync();
        var (product, sku) = await _db.SeedMenuProductAsync(menu);
        var first = await PublishAsync(menu);
        await _service.DeliverAsync(first.PublicationId);

        var again = await PublishAsync(menu);

        again.Status.Should().Be(CatalogPublicationStatus.Unchanged);
        (await OutboxCountAsync(again.PublicationId)).Should().Be(0);
        (await _db.ScalarAsync(
            "SELECT count(*) FROM online_ordering.yemeksepeti_product_mappings WHERE product_id = $1;", product)).Should().Be(1);
        _provider.CatalogUpdates.Should().ContainSingle();
        (await _db.Mappings.FindOpenSkuForProductAsync(product)).Should().Be(sku);
    }

    [Fact]
    public async Task ConcurrentIdenticalPublicationsQueueOnlyOneDelivery()
    {
        // Published once already, so the racers find their external identifiers without touching the
        // mapping table and genuinely reach the publication step at the same time.
        var menu = await _db.SeedMenuAsync();
        var (product, _) = await _db.SeedMenuProductAsync(menu, 100m);
        await _service.DeliverAsync((await PublishAsync(menu)).PublicationId);
        await _db.SetPriceAsync(product, 105m);

        for (var round = 0; round < 5; round++)
        {
            await _db.SetPriceAsync(product, 105m + round + 1);
            var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var racers = Enumerable.Range(0, 6).Select(async _ =>
            {
                await start.Task;
                return await PublishAsync(menu);
            }).ToList();
            start.SetResult();
            var results = await Task.WhenAll(racers);

            results.Count(r => r.Status == CatalogPublicationStatus.Pending).Should().Be(1, $"round {round}");
            results.Count(r => r.Status == CatalogPublicationStatus.Unchanged).Should().Be(5, $"round {round}");
        }
    }

    [Fact]
    public async Task APriceChangeIsANewPublication()
    {
        var menu = await _db.SeedMenuAsync();
        var (product, sku) = await _db.SeedMenuProductAsync(menu, 100m);
        await _service.DeliverAsync((await PublishAsync(menu)).PublicationId);
        await _db.SetPriceAsync(product, 110m);

        var changed = await PublishAsync(menu);
        await _service.DeliverAsync(changed.PublicationId);

        changed.Status.Should().Be(CatalogPublicationStatus.Pending);
        _provider.CatalogUpdates.Last().Should().Equal(new YemeksepetiCatalogProductUpdate(sku, 110m, true, null));
    }

    [Fact]
    public async Task UnsupportedProductsAreTypedValidationErrorsAndMissingCapabilitiesAreReported()
    {
        var menu = await _db.SeedMenuAsync();
        var (withModifiers, _) = await _db.SeedMenuProductAsync(menu);
        await _db.AttachModifierGroupAsync(withModifiers);
        var (unpriced, _) = await _db.SeedMenuProductAsync(menu, price: null);
        var (fine, _) = await _db.SeedMenuProductAsync(menu);

        var summary = await PublishAsync(menu);

        summary.ItemCount.Should().Be(1);
        summary.ValidationErrors.Should().BeEquivalentTo(new[]
        {
            new CatalogValidationError(withModifiers, CatalogValidationCode.ModifiersNotSupported),
            new CatalogValidationError(unpriced, CatalogValidationCode.PriceMissing)
        });
        summary.UnsupportedCapabilities.Should().BeEquivalentTo(new[]
        {
            CatalogCapability.CreateProduct, CatalogCapability.UpdateTitle, CatalogCapability.TaxMetadata,
            CatalogCapability.Modifiers, CatalogCapability.Categories
        });
        (await _db.Mappings.FindOpenSkuForProductAsync(fine)).Should().NotBeNull();
        (await _db.Mappings.FindOpenSkuForProductAsync(withModifiers)).Should().BeNull();
    }

    [Fact]
    public async Task ADeactivatedProductIsSwitchedOffOnlyIfTheChannelAlreadyKnowsIt()
    {
        var menu = await _db.SeedMenuAsync();
        var (known, knownSku) = await _db.SeedMenuProductAsync(menu);
        await _service.DeliverAsync((await PublishAsync(menu)).PublicationId);
        await _db.SetActiveAsync(known, false);
        var (neverPublished, _) = await _db.SeedMenuProductAsync(menu, active: false);

        var summary = await PublishAsync(menu);
        await _service.DeliverAsync(summary.PublicationId);

        summary.ValidationErrors.Should().BeEmpty();
        _provider.CatalogUpdates.Last().Should().Equal(new YemeksepetiCatalogProductUpdate(knownSku, 120m, false, null));
        (await _db.Mappings.FindOpenSkuForProductAsync(neverPublished)).Should().BeNull();
    }

    [Fact]
    public async Task AnExistingMappingIsTheExternalIdentifierAndAConflictingSkuIsAnError()
    {
        var menu = await _db.SeedMenuAsync();
        var (mappedElsewhere, _) = await _db.SeedMenuProductAsync(menu);
        await _db.Mappings.MapAsync("legacy-ys-7", mappedElsewhere, DateTimeOffset.UtcNow.AddDays(-1), _manager);
        var (stolenSku, sku) = await _db.SeedMenuProductAsync(menu);
        var (owner, _) = await _db.SeedMenuProductAsync(await _db.SeedMenuAsync());
        await _db.Mappings.MapAsync(sku, owner, DateTimeOffset.UtcNow.AddDays(-1), _manager);

        var summary = await PublishAsync(menu);
        await _service.DeliverAsync(summary.PublicationId);

        summary.ValidationErrors.Should().ContainSingle()
            .Which.Should().Be(new CatalogValidationError(stolenSku, CatalogValidationCode.ExternalIdUnavailable, sku));
        _provider.CatalogUpdates.Last().Should().Equal(new YemeksepetiCatalogProductUpdate("legacy-ys-7", 120m, true, null));
    }

    [Fact]
    public async Task AFailedDeliveryIsRecordedAndRetriedAndNeverSentTwiceOnceDelivered()
    {
        var menu = await _db.SeedMenuAsync();
        await _db.SeedMenuProductAsync(menu);
        var summary = await PublishAsync(menu);

        _provider.Fail = true;
        var failing = () => _service.DeliverAsync(summary.PublicationId);
        await failing.Should().ThrowAsync<YemeksepetiPartnerApiException>();
        var afterFailure = await _db.PublicationAsync(summary.PublicationId);
        afterFailure.Status.Should().Be("Pending");
        afterFailure.Attempts.Should().Be(1);
        afterFailure.LastError.Should().Contain("HTTP 503");

        _provider.Fail = false;
        await _service.DeliverAsync(summary.PublicationId);
        await _service.DeliverAsync(summary.PublicationId);

        _provider.CatalogUpdates.Should().ContainSingle();
        (await _db.PublicationAsync(summary.PublicationId)).Status.Should().Be("Delivered");
    }

    [Fact]
    public async Task AMenuWithNothingPublishableNeverCallsTheChannel()
    {
        var menu = await _db.SeedMenuAsync();
        await _db.SeedMenuProductAsync(menu, price: null);

        var summary = await PublishAsync(menu);

        summary.Status.Should().Be(CatalogPublicationStatus.NothingToPublish);
        (await OutboxCountAsync(summary.PublicationId)).Should().Be(0);
    }

    [Fact]
    public async Task AnUnknownChannelOrMissingActorIsRefused()
    {
        var menu = await _db.SeedMenuAsync();

        var unknown = () => _service.RequestAsync("Getir", menu, _manager);
        var anonymous = () => _service.RequestAsync(YemeksepetiCatalogPublisher.ChannelName, menu, Guid.Empty);

        await unknown.Should().ThrowAsync<UnknownCatalogChannelException>();
        await anonymous.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task TheMigrationRollsBackAndReapplies()
    {
        await _db.RunFixtureAsync("147-online-catalog-publications.down.sql");
        await _db.RunFixtureAsync("147-online-catalog-publications.up.sql");
        var menu = await _db.SeedMenuAsync();
        await _db.SeedMenuProductAsync(menu);

        (await PublishAsync(menu)).Status.Should().Be(CatalogPublicationStatus.Pending);
    }
}
