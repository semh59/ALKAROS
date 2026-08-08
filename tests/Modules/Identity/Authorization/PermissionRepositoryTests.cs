using ALKAROS.Identity.Authorization;
using ALKAROS.Identity.Authorization.Tests.Fixtures;
using FluentAssertions;
using Xunit;

namespace ALKAROS.Identity.Authorization.Tests;

public sealed class PermissionRepositoryTests : IClassFixture<AuthorizationTestDatabase>
{
    private readonly PostgresPermissionRepository _repository;

    public PermissionRepositoryTests(AuthorizationTestDatabase database)
    {
        _repository = new PostgresPermissionRepository(database.DataSource);
    }

    [Fact]
    public async Task CatalogIsSeededWithCanonicalNamedPermissions()
    {
        var catalog = await _repository.GetAllAsync();

        catalog.Select(p => p.Code).Should().Contain(
            new[]
            {
                PermissionCodes.UsersManage,
                PermissionCodes.RolesManage,
                PermissionCodes.PermissionsManage,
                PermissionCodes.DeviceSessionsManage,
            });
    }

    [Fact]
    public async Task GetByCodeReturnsNullForUnknownPermission()
    {
        (await _repository.GetByCodeAsync("missing.permission")).Should().BeNull();
    }

    [Fact]
    public async Task AddAndGetByCodeRoundTrips()
    {
        await _repository.AddAsync(new PermissionEntry(Guid.NewGuid(), "orders.submit", "Submit an order"));

        var loaded = await _repository.GetByCodeAsync("orders.submit");

        loaded.Should().NotBeNull();
        loaded!.Name.Should().Be("Submit an order");
    }

    [Fact]
    public async Task AddDuplicateCodeIsRejected()
    {
        var duplicate = new PermissionEntry(Guid.NewGuid(), PermissionCodes.RolesManage, "Duplicate");

        var act = () => _repository.AddAsync(duplicate);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Permission '{PermissionCodes.RolesManage}' already exists.");
    }
}