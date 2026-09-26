using ALKAROS.Catalog.ProductCatalog;
using ALKAROS.Inventory.BalanceProjection;
using ALKAROS.Inventory.CrossChannelReservation;
using ALKAROS.Inventory.MovementLedger;
using ALKAROS.Inventory.PortionReservations.CancellationEffects;
using ALKAROS.Inventory.PortionReservations.Lifecycle;
using ALKAROS.Inventory.ReservationBalanceProjection;
using ALKAROS.Inventory.StockMaster;
using ALKAROS.Inventory.Transactions;
using ALKAROS.Inventory.WasteRecording;
using ALKAROS.Measurements;
using ALKAROS.OnlineOrdering.AvailabilityPublishing.Yemeksepeti;
using ALKAROS.OnlineOrdering.Yemeksepeti.ProductMapping;
using ALKAROS.OnlineOrdering.Yemeksepeti.StatusSync;
using ALKAROS.Secrets;
using ALKAROS.TestHelpers;
using FluentAssertions;
using Xunit;

namespace ALKAROS.OnlineOrdering.AvailabilityPublishing.Tests;

public sealed class AvailabilityTestDatabase : PgTestDatabase
{
    public AvailabilityTestDatabase() : base("alkaros_online_avail_") { }

    protected override async Task ApplySqlAsync()
    {
        foreach (var file in new[]
                 {
                     "006-catalog.up.sql", "040-wave9-schema-additions.up.sql", "053-catalog-products-row-version.up.sql",
                     "103-products-prep-time.up.sql", "059-stock-master.up.sql", "118-stock-items-reorder-point.up.sql",
                     "060-stock-movements.up.sql", "061-stock-balances.up.sql", "063-waste-records.up.sql",
                     "064-portion-reservations.up.sql", "065-reservation-balance-projection.up.sql",
                     "087-inventory-stock-balances-non-negative.up.sql", "144-yemeksepeti-product-mappings.up.sql",
                     "148-online-availability-states.up.sql"
                 })
        {
            await RunFixtureAsync(file);
        }
    }

    public async Task RunFixtureAsync(string file) =>
        await RunAsync(DataSource, await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", file)));

    public PostgresYemeksepetiProductMappingService Mappings => new(
        DataSource, new PostgresProductRepository(DataSource),
        new PostgresProductModifierGroupRepository(DataSource), new PostgresModifierGroupRepository(DataSource));

    /// <summary>A product with one mapped stock item per (onHand, multiplier) pair; returns the product.</summary>
    public async Task<Guid> SeedProductAsync(params (decimal OnHand, decimal Multiplier)[] ingredients)
    {
        var productId = Guid.NewGuid();
        await ExecAsync("INSERT INTO catalog.products (product_id, sku, name, product_type, stock_mode, active) VALUES ($1, $2, 'Kuru Fasulye', 1, 1, true);",
            productId, "KF-" + productId.ToString("N")[..8]);
        var master = new StockMasterService(
            new PostgresStockLocationRepository(DataSource), new PostgresStockItemRepository(DataSource),
            new PostgresProductStockMappingRepository(DataSource), new UnitConverter());
        foreach (var (onHand, multiplier) in ingredients)
        {
            var suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
            var location = await master.CreateLocationAsync("L-" + suffix, "Pass", StockLocationType.Kitchen);
            var item = await master.CreateStockItemAsync("S-" + suffix, "Porsiyon", StockItemType.RawMaterial, "portion", defaultLocationId: location.Id);
            await master.AssignProductToStockItemAsync(productId, item.Id, multiplier);
            await new PostgresStockBalanceRepository(DataSource).ApplyOnHandDeltaAsync(item.Id, location.Id, onHand);
        }

        return productId;
    }

    public PostgresCrossChannelPortionArbiter Arbiter()
    {
        var items = new PostgresStockItemRepository(DataSource);
        var locations = new PostgresStockLocationRepository(DataSource);
        var reservations = new PostgresPortionReservationRepository(DataSource);
        var balances = new PostgresStockBalanceRepository(DataSource);
        return new PostgresCrossChannelPortionArbiter(DataSource, balances, new PortionCancellationDecisionService(
            reservations, new PortionReservationLifecycleService(reservations, items, locations),
            new ReservationBalanceProjector(new PostgresReservationBalanceRepository(DataSource)),
            new WasteRecordingService(new PostgresInventoryTransactionRunner(DataSource), new PostgresWasteRecordRepository(DataSource),
                new PostgresStockMovementRepository(DataSource), items, locations, balances, new UnitConverter()),
            new PostgresKitchenItemStateProvider(DataSource)));
    }

    public async Task<(int Desired, int? Delivered, int Attempts, string? LastError)> StateAsync(string channel, Guid productId)
    {
        await using var command = DataSource.CreateCommand(
            "SELECT desired_quantity, delivered_quantity, delivery_attempts, last_error FROM online_ordering.availability_states WHERE channel = $1 AND product_id = $2;");
        command.Parameters.AddWithValue(channel);
        command.Parameters.AddWithValue(productId);
        await using var reader = await command.ExecuteReaderAsync();
        await reader.ReadAsync();
        return (reader.GetInt32(0), reader.IsDBNull(1) ? null : reader.GetInt32(1), reader.GetInt32(2), reader.IsDBNull(3) ? null : reader.GetString(3));
    }

    public async Task<long> StateCountAsync(string channel)
    {
        await using var command = DataSource.CreateCommand("SELECT count(*) FROM online_ordering.availability_states WHERE channel = $1;");
        command.Parameters.AddWithValue(channel);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private async Task ExecAsync(string sql, params object[] parameters)
    {
        await using var command = DataSource.CreateCommand(sql);
        foreach (var parameter in parameters)
            command.Parameters.AddWithValue(parameter);
        await command.ExecuteNonQueryAsync();
    }
}

/// <summary>A channel double: knows exactly the products it is given and records every batch it is sent.</summary>
public sealed class RecordingChannel : IAvailabilityChannelPublisher
{
    public RecordingChannel(string channel) => Channel = channel;

    public string Channel { get; }

    public bool IsEnabled { get; set; } = true;

    public int MaxBatchSize { get; set; } = 100;

    public List<PublishedChannelProduct> Products { get; } = [];

    public List<IReadOnlyList<ChannelAvailability>> Batches { get; } = [];

    public bool Fail { get; set; }

    public TaskCompletionSource? Gate { get; set; }

    public Task<IReadOnlyList<PublishedChannelProduct>> PublishedProductsAsync(int limit, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<PublishedChannelProduct>>(Products.Take(limit).ToList());

    public async Task PublishAsync(IReadOnlyList<ChannelAvailability> availability, CancellationToken cancellationToken = default)
    {
        if (Gate is { } gate)
            await gate.Task;
        if (Fail)
            throw new InvalidOperationException("channel unavailable");
        lock (Batches)
            Batches.Add(availability);
    }
}

public sealed class AvailabilityPublicationTests : IClassFixture<AvailabilityTestDatabase>
{
    private readonly AvailabilityTestDatabase _db;

    public AvailabilityPublicationTests(AvailabilityTestDatabase db) => _db = db;

    private static string ChannelName() => "Test-" + Guid.NewGuid().ToString("N")[..8];

    private static RecordingChannel ChannelFor(params Guid[] products)
    {
        var channel = new RecordingChannel(ChannelName());
        channel.Products.AddRange(products.Select(p => new PublishedChannelProduct(p, "ext-" + p.ToString("N")[..10])));
        return channel;
    }

    private AvailabilityPublicationService Service(params IAvailabilityChannelPublisher[] channels) => new(_db.DataSource, channels);

    [Fact]
    public async Task TheLastPortionGoingToAnotherChannelReachesThisChannelExactlyOnce()
    {
        var product = await _db.SeedProductAsync((1m, 1m));
        var channel = ChannelFor(product);
        var service = Service(channel);

        await service.RunPassAsync();
        await using (var connection = await _db.DataSource.OpenConnectionAsync())
        await using (var transaction = await connection.BeginTransactionAsync())
        {
            var held = await _db.Arbiter().ReserveAsync(new CrossChannelReservationRequest(
                ReservationChannel.Qr, "qr-1", Guid.NewGuid(), Guid.NewGuid(),
                [new CrossChannelReservationLine(Guid.NewGuid(), product, 1m)]), connection, transaction);
            held.Outcome.Should().Be(CrossChannelReservationOutcome.Reserved);
            await transaction.CommitAsync();
        }

        await service.RunPassAsync();
        await service.RunPassAsync();

        channel.Batches.SelectMany(b => b).Select(a => a.Quantity).Should().Equal(1, 0);
        (await _db.StateAsync(channel.Channel, product)).Should().Be((0, (int?)0, 0, (string?)null));
    }

    [Fact]
    public async Task AProductSellsAsManyUnitsAsItsScarcestIngredientAllows()
    {
        var product = await _db.SeedProductAsync((10m, 2m), (3m, 1m), (7.9m, 2.5m));
        var channel = ChannelFor(product);

        await Service(channel).RunPassAsync();

        channel.Batches.Single().Should().Equal(new ChannelAvailability(channel.Products[0].ExternalId, 3));
    }

    [Fact]
    public async Task ADelayedOlderObservationNeverOverwritesANewerState()
    {
        var product = await _db.SeedProductAsync((5m, 1m));
        var channel = ChannelFor(product);
        var service = Service(channel);
        var published = channel.Products[0];

        (await service.RecordObservationAsync(channel.Channel, published, 2, version: 50)).Should().Be(1);
        (await service.RecordObservationAsync(channel.Channel, published, 9, version: 40)).Should().Be(0);
        (await service.RecordObservationAsync(channel.Channel, published, 2, version: 60)).Should().Be(0);
        (await service.RecordObservationAsync(channel.Channel, published, 7, version: 55)).Should().Be(0, "55 is older than the recorded 60");
        await service.DeliverAsync(channel);

        channel.Batches.Single().Single().Quantity.Should().Be(2);
    }

    [Fact]
    public async Task AChangeDuringADeliveryIsSentNextAndAnOldDeliveryNeverWinsLater()
    {
        var product = await _db.SeedProductAsync((5m, 1m));
        var channel = ChannelFor(product);
        var service = Service(channel);
        var published = channel.Products[0];
        await service.RecordObservationAsync(channel.Channel, published, 2, version: 10);

        channel.Gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var delivering = service.DeliverAsync(channel);
        var newer = service.RecordObservationAsync(channel.Channel, published, 0, version: 11);
        await Task.Delay(200);
        channel.Gate.SetResult();
        await delivering;
        (await newer).Should().Be(1);
        channel.Gate = null;
        await service.DeliverAsync(channel);

        channel.Batches.Select(b => b.Single().Quantity).Should().Equal(2, 0);
        (await _db.StateAsync(channel.Channel, product)).Delivered.Should().Be(0);
    }

    [Fact]
    public async Task OnePassSendsOneBoundedBatchPerChannel()
    {
        var products = new List<Guid>();
        for (var i = 0; i < 7; i++)
            products.Add(await _db.SeedProductAsync((3m, 1m)));
        var channel = ChannelFor([.. products]);
        channel.MaxBatchSize = 5;
        var service = Service(channel);

        (await service.RunPassAsync()).Should().Be(new AvailabilityPassResult(7, 5));
        (await service.RunPassAsync()).Should().Be(new AvailabilityPassResult(0, 2));
        (await service.RunPassAsync()).Should().Be(new AvailabilityPassResult(0, 0));

        channel.Batches.Select(b => b.Count).Should().Equal(5, 2);
    }

    [Fact]
    public async Task ParallelDeliveriesNeverSendAProductTwice()
    {
        var products = new List<Guid>();
        for (var i = 0; i < 12; i++)
            products.Add(await _db.SeedProductAsync((2m, 1m)));
        var channel = ChannelFor([.. products]);
        channel.MaxBatchSize = 3;
        var service = Service(channel);
        await service.RefreshAsync(channel);
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var workers = Enumerable.Range(0, 6).Select(async _ =>
        {
            await start.Task;
            var sent = 0;
            int batch;
            while ((batch = await service.DeliverAsync(channel)) > 0)
                sent += batch;
            return sent;
        }).ToList();
        start.SetResult();

        (await Task.WhenAll(workers)).Sum().Should().Be(12);
        channel.Batches.SelectMany(b => b).Select(a => a.ExternalId).Should().OnlyHaveUniqueItems().And.HaveCount(12);
    }

    [Fact]
    public async Task AFailedDeliveryIsRecordedRetriedAndSurfacesAsDivergence()
    {
        var product = await _db.SeedProductAsync((4m, 1m));
        var channel = ChannelFor(product);
        var service = Service(channel);
        channel.Fail = true;

        for (var attempt = 0; attempt < 3; attempt++)
        {
            var pass = () => service.RunPassAsync();
            await pass.Should().ThrowAsync<InvalidOperationException>();
        }

        var failing = await _db.StateAsync(channel.Channel, product);
        failing.Attempts.Should().Be(3);
        failing.LastError.Should().Contain("channel unavailable");
        (await service.FindDivergencesAsync(TimeSpan.FromHours(1), failedAttempts: 3, limit: 50))
            .Should().ContainSingle(d => d.ProductId == product && d.DesiredQuantity == 4 && d.DeliveredQuantity == null);

        channel.Fail = false;
        await service.RunPassAsync();

        (await _db.StateAsync(channel.Channel, product)).Should().Be((4, (int?)4, 0, (string?)null));
        (await service.FindDivergencesAsync(TimeSpan.Zero, failedAttempts: 1, limit: 50))
            .Should().NotContain(d => d.ProductId == product);
    }

    [Fact]
    public async Task ADisabledChannelIsNeitherComputedNorCalled()
    {
        var product = await _db.SeedProductAsync((4m, 1m));
        var channel = ChannelFor(product);
        channel.IsEnabled = false;

        (await Service(channel).RunPassAsync()).Should().Be(new AvailabilityPassResult(0, 0));

        channel.Batches.Should().BeEmpty();
        (await _db.StateCountAsync(channel.Channel)).Should().Be(0);
    }

    [Fact]
    public async Task TheYemeksepetiChannelKnowsMappedProductsSendsQuantityOnlyAndNeedsCredentials()
    {
        var product = await _db.SeedProductAsync((6m, 1m));
        var sku = "ys-avail-" + product.ToString("N")[..8];
        await _db.Mappings.MapAsync(sku, product, DateTimeOffset.UtcNow.AddMinutes(-1), Guid.NewGuid());
        var client = new RecordingPartnerClient();
        var secrets = new InMemorySecretProvider();
        var publisher = new YemeksepetiAvailabilityPublisher(_db.Mappings, client, secrets);

        publisher.IsEnabled.Should().BeFalse();
        secrets.Set(YemeksepetiPartnerHttpClient.ClientId, "client-a");
        publisher.IsEnabled.Should().BeTrue();

        var known = await publisher.PublishedProductsAsync(2000);
        known.Should().Contain(new PublishedChannelProduct(product, sku));
        await publisher.PublishAsync([new ChannelAvailability(sku, 0)]);

        client.Updates.Single().Should().Equal(new YemeksepetiCatalogProductUpdate(sku, null, null, 0m));
    }

    [Fact]
    public async Task TheMigrationRollsBackAndReapplies()
    {
        await _db.RunFixtureAsync("148-online-availability-states.down.sql");
        await _db.RunFixtureAsync("148-online-availability-states.up.sql");
        var product = await _db.SeedProductAsync((1m, 1m));
        var channel = ChannelFor(product);

        (await Service(channel).RunPassAsync()).Should().Be(new AvailabilityPassResult(1, 1));
    }

    private sealed class RecordingPartnerClient : IYemeksepetiPartnerClient
    {
        public List<IReadOnlyList<YemeksepetiCatalogProductUpdate>> Updates { get; } = [];

        public Task UpdateOrderStatusAsync(YemeksepetiStatusUpdateRequested update, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<string?> UpdateVendorCatalogAsync(IReadOnlyList<YemeksepetiCatalogProductUpdate> products, CancellationToken cancellationToken = default)
        {
            Updates.Add(products);
            return Task.FromResult<string?>(null);
        }
    }
}
