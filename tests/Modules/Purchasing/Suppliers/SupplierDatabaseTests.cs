using System;
using System.IO;
using System.Threading.Tasks;
using ALKAROS.TestHelpers;
using FluentAssertions;
using Xunit;

namespace ALKAROS.Purchasing.Suppliers.Tests;

public sealed class SupplierTestDb : PgTestDatabase
{
    public SupplierTestDb() : base("alkaros_pur_supplier_test_") { }

    protected override async Task ApplySqlAsync()
    {
        var migration065 = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "065-suppliers.up.sql");
        var sql065 = await File.ReadAllTextAsync(migration065);
        await RunAsync(DataSource, sql065);
    }

    public async Task RollbackMigration065Async()
    {
        var downSqlPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "065-suppliers.down.sql");
        var downSql = await File.ReadAllTextAsync(downSqlPath);
        await RunAsync(DataSource, downSql);
    }

    public async Task ReapplyMigration065Async()
    {
        var migration065 = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "065-suppliers.up.sql");
        var sql065 = await File.ReadAllTextAsync(migration065);
        await RunAsync(DataSource, sql065);
    }
}

public sealed class SupplierDatabaseTests : IClassFixture<SupplierTestDb>
{
    private readonly SupplierTestDb _db;
    private readonly PostgresSupplierRepository _repo;
    private readonly SupplierService _service;

    public SupplierDatabaseTests(SupplierTestDb db)
    {
        _db = db;
        _repo = new PostgresSupplierRepository(db.DataSource);
        _service = new SupplierService(_repo);
    }

    [Fact]
    public async Task CreateAndRetrieveSupplierPersistsToPostgres()
    {
        var code = "SUP-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var cmd = new CreateSupplierCommand(
            Code: code,
            Name: "Organic Veggies",
            TaxNumber: "111" + Guid.NewGuid().ToString("N")[..7],
            TaxOffice: "Kadikoy",
            Phone: "+905550001122",
            Email: "order@organicveggies.com");

        var created = await _service.CreateSupplierAsync(cmd);
        created.Should().NotBeNull();

        var fetched = await _repo.GetByIdAsync(created.Id);
        fetched.Should().NotBeNull();
        fetched!.Code.Should().Be(code);
        fetched.Name.Should().Be("Organic Veggies");
        fetched.TaxNumber.Should().Be(cmd.TaxNumber);
        fetched.Active.Should().BeTrue();

        var fetchedByCode = await _repo.GetByCodeAsync(code);
        fetchedByCode.Should().NotBeNull();
        fetchedByCode!.Id.Should().Be(created.Id);
    }

    [Fact]
    public async Task DuplicateSupplierCodeThrowsDuplicateSupplierCodeException()
    {
        var code = "SUP-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var cmd1 = new CreateSupplierCommand(Code: code, Name: "Supplier One");
        await _service.CreateSupplierAsync(cmd1);

        var cmd2 = new CreateSupplierCommand(Code: code, Name: "Supplier Two");
        var act = async () => await _service.CreateSupplierAsync(cmd2);

        await act.Should().ThrowAsync<DuplicateSupplierCodeException>();
    }

    [Fact]
    public async Task DuplicateTaxNumberThrowsDuplicateSupplierTaxNumberException()
    {
        var taxNum = "TAX-" + Guid.NewGuid().ToString("N")[..8];
        var cmd1 = new CreateSupplierCommand(
            Code: "SUP-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(),
            Name: "Supplier One",
            TaxNumber: taxNum);
        await _service.CreateSupplierAsync(cmd1);

        var cmd2 = new CreateSupplierCommand(
            Code: "SUP-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(),
            Name: "Supplier Two",
            TaxNumber: taxNum);
        var act = async () => await _service.CreateSupplierAsync(cmd2);

        await act.Should().ThrowAsync<DuplicateSupplierTaxNumberException>();
    }

    [Fact]
    public async Task MultipleSuppliersWithNullTaxNumberAreAllowed()
    {
        var cmd1 = new CreateSupplierCommand(
            Code: "SUP-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(),
            Name: "Supplier No Tax 1",
            TaxNumber: null);
        var s1 = await _service.CreateSupplierAsync(cmd1);

        var cmd2 = new CreateSupplierCommand(
            Code: "SUP-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(),
            Name: "Supplier No Tax 2",
            TaxNumber: null);
        var s2 = await _service.CreateSupplierAsync(cmd2);

        s1.Should().NotBeNull();
        s2.Should().NotBeNull();
    }

    [Fact]
    public async Task InactiveSupplierRejectsOrders()
    {
        var code = "SUP-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var cmd = new CreateSupplierCommand(Code: code, Name: "Supplier Inactive Test", Active: true);
        var supplier = await _service.CreateSupplierAsync(cmd);

        // Initially active, order can be accepted
        var actActive = async () => await _service.AssertSupplierCanAcceptOrdersAsync(supplier.Id);
        await actActive.Should().NotThrowAsync();

        // Deactivate
        await _service.DeactivateSupplierAsync(supplier.Id);

        // Now order acceptance must fail
        var actInactive = async () => await _service.AssertSupplierCanAcceptOrdersAsync(supplier.Id);
        await actInactive.Should().ThrowAsync<InactiveSupplierException>()
            .WithMessage("*inactive*");

        // Reactivate
        await _service.ActivateSupplierAsync(supplier.Id);
        await actActive.Should().NotThrowAsync();
    }

    [Fact]
    public async Task GetSupplierViewRespectsRoleMasking()
    {
        var code = "SUP-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var cmd = new CreateSupplierCommand(
            Code: code,
            Name: "Bakery Supplies",
            TaxNumber: "9876543210",
            TaxOffice: "Kadikoy",
            Phone: "+905551112233",
            Email: "orders@bakery.com");
        var supplier = await _service.CreateSupplierAsync(cmd);

        // Unauthorized user (e.g. Waiter)
        var waiterDto = await _service.GetSupplierViewAsync(supplier.Id, "Waiter");
        waiterDto.IsMasked.Should().BeTrue();
        waiterDto.TaxNumber.Should().Be("******3210");
        waiterDto.Phone.Should().Be("***-***2233");
        waiterDto.Email.Should().Be("o***@bakery.com");

        // Authorized user (e.g. Manager)
        var managerDto = await _service.GetSupplierViewAsync(supplier.Id, "Manager");
        managerDto.IsMasked.Should().BeFalse();
        managerDto.TaxNumber.Should().Be("9876543210");
        managerDto.Phone.Should().Be("+905551112233");
        managerDto.Email.Should().Be("orders@bakery.com");
    }

    [Fact]
    public async Task Migration065RollbackAndReapplyWorksCleanly()
    {
        await _db.RollbackMigration065Async();
        await _db.ReapplyMigration065Async();

        var code = "SUP-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var supplier = await _service.CreateSupplierAsync(new CreateSupplierCommand(code, "Reapply Test"));
        supplier.Should().NotBeNull();
    }
}
