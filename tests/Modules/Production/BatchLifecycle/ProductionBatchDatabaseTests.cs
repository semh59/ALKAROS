using System;
using System.IO;
using System.Threading.Tasks;
using ALKAROS.Production.BatchLifecycle;
using ALKAROS.TestHelpers;
using FluentAssertions;
using Npgsql;
using Xunit;

namespace ALKAROS.Production.BatchLifecycle.Tests;

public sealed class ProductionBatchTestDb : PgTestDatabase
{
    public ProductionBatchTestDb() : base("alkaros_prd_batch_test_") { }

    protected override async Task ApplySqlAsync()
    {
        // 057: Unit conversions
        var sql057 = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "057-unit-conversions.up.sql"));
        await RunAsync(DataSource, sql057);

        // 058: Recipe versions
        var sql058 = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "058-recipe-versions.up.sql"));
        await RunAsync(DataSource, sql058);

        // 059: Stock master
        var sql059 = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "059-stock-master.up.sql"));
        await RunAsync(DataSource, sql059);

        // Catalog products for daily menu items FK
        const string catalogSql = """
            CREATE SCHEMA IF NOT EXISTS catalog;
            CREATE TABLE IF NOT EXISTS catalog.products (
                product_id UUID PRIMARY KEY,
                code VARCHAR(64) NOT NULL UNIQUE,
                name VARCHAR(255) NOT NULL,
                base_price NUMERIC(18,2) NOT NULL DEFAULT 0,
                category_id UUID NULL,
                is_active BOOLEAN NOT NULL DEFAULT true,
                created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
            );
            """;
        await RunAsync(DataSource, catalogSql);

        // 067: Daily menus
        var sql067 = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "067-daily-menus.up.sql"));
        await RunAsync(DataSource, sql067);

        // 071: Production batches
        var sql071 = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "071-production-batches.up.sql"));
        await RunAsync(DataSource, sql071);
    }

    public async Task RollbackMigration071Async()
    {
        var downSql = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "071-production-batches.down.sql"));
        await RunAsync(DataSource, downSql);
    }

    public async Task ReapplyMigration071Async()
    {
        var upSql = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "071-production-batches.up.sql"));
        await RunAsync(DataSource, upSql);
    }
}

public sealed class ProductionBatchDatabaseTests : IClassFixture<ProductionBatchTestDb>
{
    private readonly ProductionBatchTestDb _db;
    private readonly PostgresProductionBatchRepository _repo;
    private readonly ProductionBatchService _service;

    public ProductionBatchDatabaseTests(ProductionBatchTestDb db)
    {
        _db = db;
        _repo = new PostgresProductionBatchRepository(db.DataSource);
        _service = new ProductionBatchService(_repo);
    }

    private async Task<(Guid recipeId, Guid versionId)> SeedRecipeVersionAsync()
    {
        var recipeId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var recipeCode = "RCP-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

        await using var conn = await _db.DataSource.OpenConnectionAsync();
        const string sql = """
            INSERT INTO recipe.recipes (id, code, name, created_at, row_version)
            VALUES (@recipeId, @code, 'Test Soup Recipe', NOW(), 1);

            INSERT INTO recipe.recipe_versions (
                id, recipe_id, version_number, status, yield_quantity, yield_unit_code, created_at, row_version
            ) VALUES (
                @versionId, @recipeId, 1, 'Active', 10.0, 'portion', NOW(), 1
            );
            """;
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("recipeId", recipeId);
        cmd.Parameters.AddWithValue("code", recipeCode);
        cmd.Parameters.AddWithValue("versionId", versionId);
        await cmd.ExecuteNonQueryAsync();

        return (recipeId, versionId);
    }

    private async Task<Guid> SeedStockLocationAsync()
    {
        var locId = Guid.NewGuid();
        var code = "LOC-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        await using var conn = await _db.DataSource.OpenConnectionAsync();
        const string sql = """
            INSERT INTO inventory.stock_locations (id, code, name, location_type, is_active, row_version)
            VALUES (@id, @code, 'Kitchen Prep Station', 'Kitchen', true, 1);
            """;
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", locId);
        cmd.Parameters.AddWithValue("code", code);
        await cmd.ExecuteNonQueryAsync();
        return locId;
    }

    [Fact]
    public async Task RollbackAndReapplyMigration071SucceedsWithoutErrors()
    {
        await _db.RollbackMigration071Async();
        await _db.ReapplyMigration071Async();
    }

    [Fact]
    public async Task CreateAndRetrieveBatchPersistsToPostgres()
    {
        var (_, versionId) = await SeedRecipeVersionAsync();
        var locationId = await SeedStockLocationAsync();
        var batchNumber = "PB-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

        var cmd = new CreateProductionBatchCommand(
            BatchNumber: batchNumber,
            RecipeVersionId: versionId,
            PlannedQuantity: 40.0m,
            PortionUnitCode: "portion",
            DestinationLocationId: locationId,
            Notes: "Fresh morning batch");

        var created = await _service.CreateBatchAsync(cmd);

        created.Id.Should().NotBeEmpty();
        created.BatchNumber.Should().Be(batchNumber);
        created.RecipeVersionId.Should().Be(versionId);
        created.DestinationLocationId.Should().Be(locationId);
        created.Status.Should().Be(ProductionBatchStatus.Planned);
        created.PlannedQuantity.Should().Be(40.0m);
        created.ActualQuantity.Should().Be(0m);
        created.RowVersion.Should().Be(1);

        var retrieved = await _service.GetBatchAsync(created.Id);
        retrieved.Should().NotBeNull();
        retrieved!.BatchNumber.Should().Be(batchNumber);
        retrieved.RecipeVersionId.Should().Be(versionId);
        retrieved.Status.Should().Be(ProductionBatchStatus.Planned);

        var retrievedByNumber = await _service.GetBatchByNumberAsync(batchNumber);
        retrievedByNumber.Should().NotBeNull();
        retrievedByNumber!.Id.Should().Be(created.Id);
    }

    [Fact]
    public async Task StartAndCompleteBatchPersistsTransitionsToPostgres()
    {
        var (_, versionId) = await SeedRecipeVersionAsync();
        var batchNumber = "PB-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

        var created = await _service.CreateBatchAsync(new CreateProductionBatchCommand(
            BatchNumber: batchNumber,
            RecipeVersionId: versionId,
            PlannedQuantity: 50.0m));

        // Start batch: Planned -> InProgress
        var startedAt = DateTimeOffset.UtcNow;
        var inProgress = await _service.StartBatchAsync(new StartProductionBatchCommand(
            BatchId: created.Id,
            StartedAt: startedAt));

        inProgress.Status.Should().Be(ProductionBatchStatus.InProgress);
        inProgress.StartedAt.Should().BeCloseTo(startedAt, TimeSpan.FromSeconds(1));
        inProgress.RowVersion.Should().Be(2);

        // Complete batch: InProgress -> Completed
        var completedAt = DateTimeOffset.UtcNow;
        var completed = await _service.CompleteBatchAsync(new CompleteProductionBatchCommand(
            BatchId: created.Id,
            ActualQuantity: 48.0m,
            CompletedAt: completedAt));

        completed.Status.Should().Be(ProductionBatchStatus.Completed);
        completed.ActualQuantity.Should().Be(48.0m);
        completed.CompletedAt.Should().BeCloseTo(completedAt, TimeSpan.FromSeconds(1));
        completed.ProducedAt.Should().BeCloseTo(completedAt, TimeSpan.FromSeconds(1));
        completed.RowVersion.Should().Be(3);

        // Verification from database
        var inDb = await _service.GetBatchAsync(created.Id);
        inDb!.Status.Should().Be(ProductionBatchStatus.Completed);
        inDb.ActualQuantity.Should().Be(48.0m);
    }

    [Fact]
    public async Task CancelPlannedBatchPersistsCancelledStatusAndReason()
    {
        var (_, versionId) = await SeedRecipeVersionAsync();
        var batchNumber = "PB-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

        var created = await _service.CreateBatchAsync(new CreateProductionBatchCommand(
            BatchNumber: batchNumber,
            RecipeVersionId: versionId,
            PlannedQuantity: 30.0m));

        var cancelTime = DateTimeOffset.UtcNow;
        var cancelled = await _service.CancelBatchAsync(new CancelProductionBatchCommand(
            BatchId: created.Id,
            Reason: "Customer event cancelled",
            CancelledAt: cancelTime));

        cancelled.Status.Should().Be(ProductionBatchStatus.Cancelled);
        cancelled.CancellationReason.Should().Be("Customer event cancelled");
        cancelled.CancelledAt.Should().BeCloseTo(cancelTime, TimeSpan.FromSeconds(1));
        cancelled.RowVersion.Should().Be(2);

        var inDb = await _service.GetBatchAsync(created.Id);
        inDb!.Status.Should().Be(ProductionBatchStatus.Cancelled);
    }

    [Fact]
    public async Task CancelInProgressBatchPersistsCancelledStatusAndReason()
    {
        var (_, versionId) = await SeedRecipeVersionAsync();
        var batchNumber = "PB-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

        var created = await _service.CreateBatchAsync(new CreateProductionBatchCommand(
            BatchNumber: batchNumber,
            RecipeVersionId: versionId,
            PlannedQuantity: 25.0m));

        await _service.StartBatchAsync(new StartProductionBatchCommand(created.Id));

        var cancelled = await _service.CancelBatchAsync(new CancelProductionBatchCommand(
            BatchId: created.Id,
            Reason: "Kitchen power outage"));

        cancelled.Status.Should().Be(ProductionBatchStatus.Cancelled);
        cancelled.CancellationReason.Should().Be("Kitchen power outage");
        cancelled.RowVersion.Should().Be(3);
    }

    [Fact]
    public async Task DuplicateBatchNumberThrowsDuplicateNumberException()
    {
        var (_, versionId) = await SeedRecipeVersionAsync();
        var batchNumber = "PB-DUP-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

        await _service.CreateBatchAsync(new CreateProductionBatchCommand(
            BatchNumber: batchNumber,
            RecipeVersionId: versionId,
            PlannedQuantity: 10m));

        var act = () => _service.CreateBatchAsync(new CreateProductionBatchCommand(
            BatchNumber: batchNumber,
            RecipeVersionId: versionId,
            PlannedQuantity: 20m));

        await act.Should().ThrowAsync<ProductionBatchDuplicateNumberException>()
            .Where(e => e.BatchNumber == batchNumber);
    }

    [Fact]
    public async Task NonExistentRecipeVersionThrowsPostgresException()
    {
        var nonExistentVersionId = Guid.NewGuid();
        var batchNumber = "PB-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

        var act = () => _service.CreateBatchAsync(new CreateProductionBatchCommand(
            BatchNumber: batchNumber,
            RecipeVersionId: nonExistentVersionId,
            PlannedQuantity: 10m));

        await act.Should().ThrowAsync<PostgresException>()
            .Where(e => e.SqlState == PostgresErrorCodes.ForeignKeyViolation);
    }

    [Fact]
    public async Task ConcurrentUpdateThrowsConcurrencyException()
    {
        var (_, versionId) = await SeedRecipeVersionAsync();
        var batchNumber = "PB-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

        var created = await _service.CreateBatchAsync(new CreateProductionBatchCommand(
            BatchNumber: batchNumber,
            RecipeVersionId: versionId,
            PlannedQuantity: 50m));

        // Retrieve two references at version 1
        var batch1 = await _repo.GetByIdAsync(created.Id);
        var batch2 = await _repo.GetByIdAsync(created.Id);

        batch1!.Start();
        await _repo.UpdateAsync(batch1); // increments to version 2

        // batch2 attempts to update with stale version 1
        batch2!.Cancel("Stale cancellation");
        var act = () => _repo.UpdateAsync(batch2);

        await act.Should().ThrowAsync<ProductionBatchConcurrencyException>()
            .Where(e => e.BatchId == created.Id && e.ExpectedVersion == 1);
    }

    [Fact]
    public async Task ReassignRecipeVersionRejectsAttemptAndThrowsRecipeVersionImmutableException()
    {
        var (_, versionId) = await SeedRecipeVersionAsync();
        var (_, anotherVersionId) = await SeedRecipeVersionAsync();
        var batchNumber = "PB-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

        var created = await _service.CreateBatchAsync(new CreateProductionBatchCommand(
            BatchNumber: batchNumber,
            RecipeVersionId: versionId,
            PlannedQuantity: 30m));

        // Start and complete batch
        await _service.StartBatchAsync(new StartProductionBatchCommand(created.Id));
        await _service.CompleteBatchAsync(new CompleteProductionBatchCommand(created.Id, 29m));

        // Attempting to reassign RecipeVersion on completed batch
        var act = () => _service.ReassignRecipeVersionAsync(new ReassignRecipeVersionCommand(
            BatchId: created.Id,
            NewRecipeVersionId: anotherVersionId));

        await act.Should().ThrowAsync<RecipeVersionImmutableException>()
            .Where(e => e.BatchId == created.Id
                     && e.CurrentRecipeVersionId == versionId
                     && e.AttemptedRecipeVersionId == anotherVersionId);

        // Verification: database state unmodified
        var inDb = await _service.GetBatchAsync(created.Id);
        inDb!.RecipeVersionId.Should().Be(versionId);
    }

    [Fact]
    public async Task ListBatchesAppliesFiltersCorrectly()
    {
        var (_, versionId) = await SeedRecipeVersionAsync();
        var batchNumber1 = "PB-FILTER-1-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var batchNumber2 = "PB-FILTER-2-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

        var b1 = await _service.CreateBatchAsync(new CreateProductionBatchCommand(
            BatchNumber: batchNumber1,
            RecipeVersionId: versionId,
            PlannedQuantity: 20m));

        var b2 = await _service.CreateBatchAsync(new CreateProductionBatchCommand(
            BatchNumber: batchNumber2,
            RecipeVersionId: versionId,
            PlannedQuantity: 30m));

        await _service.StartBatchAsync(new StartProductionBatchCommand(b1.Id));

        var inProgressBatches = await _service.ListBatchesAsync(new ProductionBatchFilter(Status: ProductionBatchStatus.InProgress));
        inProgressBatches.Should().Contain(x => x.Id == b1.Id);
        inProgressBatches.Should().NotContain(x => x.Id == b2.Id);

        var plannedBatches = await _service.ListBatchesAsync(new ProductionBatchFilter(Status: ProductionBatchStatus.Planned));
        plannedBatches.Should().Contain(x => x.Id == b2.Id);
        plannedBatches.Should().NotContain(x => x.Id == b1.Id);

        var byRecipe = await _service.ListBatchesAsync(new ProductionBatchFilter(RecipeVersionId: versionId));
        byRecipe.Should().Contain(x => x.Id == b1.Id);
        byRecipe.Should().Contain(x => x.Id == b2.Id);
    }
}
