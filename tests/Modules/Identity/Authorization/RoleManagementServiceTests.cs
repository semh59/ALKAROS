using ALKAROS.Identity.Authorization;
using ALKAROS.Identity.Authorization.Tests.Fixtures;
using FluentAssertions;
using Xunit;

namespace ALKAROS.Identity.Authorization.Tests;

/// <summary>
/// Every protected command must have a named permission; denied actors must
/// not change any state (V1-IAM-002 acceptance).
/// </summary>
public sealed class RoleManagementServiceTests : IClassFixture<AuthorizationTestDatabase>
{
    private readonly AuthorizationTestDatabase _database;
    private readonly PostgresRoleRepository _roles;
    private readonly RoleManagementService _service;

    public RoleManagementServiceTests(AuthorizationTestDatabase database)
    {
        _database = database;
        _roles = new PostgresRoleRepository(database.DataSource);
        var permissions = new PostgresPermissionRepository(database.DataSource);
        var authorization = new AuthorizationService(
            _roles, new PostgresDenialEventSink(database.DataSource));
        _service = new RoleManagementService(authorization, _roles, permissions);
    }

    private async Task<(Guid Manager, Guid Staff)> InsertActorsAsync()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var manager = await _database.InsertUserAsync("manager_" + suffix);
        await _database.SeedRoleWithPermissionAsync("admin_" + suffix, manager, PermissionCodes.RolesManage);
        var staff = await _database.InsertUserAsync("staff_" + suffix);
        return (manager, staff);
    }

    [Fact]
    public async Task AllowedActorCreatesRoleAndItPersists()
    {
        var (manager, _) = await InsertActorsAsync();
        var code = "waiter_" + Guid.NewGuid().ToString("N")[..8];

        await _service.CreateRoleAsync(manager, code, "Waiter");

        var stored = await _roles.GetByCodeAsync(code);
        stored.Should().NotBeNull();
        stored!.Name.Should().Be("Waiter");
    }

    [Fact]
    public async Task DeniedActorCannotCreateRoleAndStateIsUnchanged()
    {
        var (_, staff) = await InsertActorsAsync();
        var code = "waiter_" + Guid.NewGuid().ToString("N")[..8];
        var before = await _database.CountAsync("identity.roles");

        var act = () => _service.CreateRoleAsync(staff, code, "Waiter");

        await act.Should().ThrowAsync<AuthorizationDeniedException>();
        (await _database.CountAsync("identity.roles")).Should().Be(before);
        (await _roles.GetByCodeAsync(code)).Should().BeNull();
    }

[Fact]
    public async Task AllowedActorAssignsPermissionToItsOwnRole()
    {
        var (manager, _) = await InsertActorsAsync();

        var managerRole = await _roles.GetRoleIdsForUserAsync(manager);
        var roleId = managerRole.Single();

        await _service.AssignPermissionAsync(manager, roleId, PermissionCodes.UsersManage);

        var codes = await _roles.GetPermissionCodesForUserAsync(manager);
        codes.Should().Contain(PermissionCodes.RolesManage);
        codes.Should().Contain(PermissionCodes.UsersManage);
    }

    [Fact]
    public async Task DeniedActorCannotAssignPermissionAndNoRowPersists()
    {
        var (manager, staff) = await InsertActorsAsync();
        var roleId = (await _roles.GetRoleIdsForUserAsync(manager)).Single();
        var before = await _database.CountAsync("identity.role_permissions");

        var act = () => _service.AssignPermissionAsync(
            staff, roleId, PermissionCodes.UsersManage);

        await act.Should().ThrowAsync<AuthorizationDeniedException>();
        (await _database.CountAsync("identity.role_permissions")).Should().Be(before);
    }

    [Fact]
    public async Task AllowedActorCanRevokePermissionFromRole()
    {
        var (manager, _) = await InsertActorsAsync();
        var roleId = (await _roles.GetRoleIdsForUserAsync(manager)).Single();

        await _service.RevokePermissionAsync(manager, roleId, PermissionCodes.RolesManage);

        var codes = await _roles.GetPermissionCodesForUserAsync(manager);
        codes.Should().NotContain(PermissionCodes.RolesManage);
        (await _roles.GetRoleIdsForUserAsync(manager)).Should().Contain(roleId);
    }

    [Fact]
    public async Task AllowedActorAssignsUserToRole()
    {
        var (manager, staff) = await InsertActorsAsync();
        var roleId = (await _roles.GetRoleIdsForUserAsync(manager)).Single();

        await _service.AssignUserAsync(manager, staff, roleId);

        (await _roles.GetRoleIdsForUserAsync(staff)).Should().Contain(roleId);
    }

    [Fact]
    public async Task DeniedActorCannotAssignUserAndStateIsUnchanged()
    {
        var (manager, staff) = await InsertActorsAsync();
        var roleId = (await _roles.GetRoleIdsForUserAsync(manager)).Single();
        var before = await _database.CountAsync("identity.user_roles");

        var act = () => _service.AssignUserAsync(staff, manager, roleId);

        await act.Should().ThrowAsync<AuthorizationDeniedException>();
        (await _database.CountAsync("identity.user_roles")).Should().Be(before);
    }

    [Fact]
    public async Task AllowedActorCanAddPermissionToCatalog()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var manager = await _database.InsertUserAsync("catalog_mgr_" + suffix);
        await _database.SeedRoleWithPermissionAsync(
            "admin_" + suffix,
            manager,
            PermissionCodes.RolesManage,
            PermissionCodes.PermissionsManage);
        var code = "orders.submit_" + suffix;

        await _service.AddPermissionAsync(manager, code, "Submit an order");

        var stored = await new PostgresPermissionRepository(_database.DataSource)
            .GetByCodeAsync(code);
        stored.Should().NotBeNull();
    }

    [Fact]
    public async Task DeniedActorCannotAddPermissionAndNoRowPersists()
    {
        var (_, staff) = await InsertActorsAsync();
        var before = await _database.CountAsync("identity.permissions");

        var act = () => _service.AddPermissionAsync(
            staff, "orders.submit", "Submit an order");

        await act.Should().ThrowAsync<AuthorizationDeniedException>();
        (await _database.CountAsync("identity.permissions")).Should().Be(before);
    }
}