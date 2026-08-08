using ALKAROS.Identity.Authorization;
using ALKAROS.Identity.Authorization.Tests.Fixtures;
using FluentAssertions;
using Xunit;

namespace ALKAROS.Identity.Authorization.Tests;

public sealed class AuthorizationServiceTests : IClassFixture<AuthorizationTestDatabase>
{
    private readonly AuthorizationTestDatabase _database;
    private readonly AuthorizationService _service;

    public AuthorizationServiceTests(AuthorizationTestDatabase database)
    {
        _database = database;
        var roles = new PostgresRoleRepository(database.DataSource);
        _service = new AuthorizationService(roles, new PostgresDenialEventSink(database.DataSource));
    }

    private Task<Guid> InsertUserAsync(bool active = true)
        => _database.InsertUserAsync("actor_" + Guid.NewGuid().ToString("N")[..20], active);

    [Fact]
    public async Task UserWithAssignedPermissionIsAuthorized()
    {
        var userId = await InsertUserAsync();
        await _database.SeedRoleWithPermissionAsync(
            "cashier_" + Guid.NewGuid().ToString("N")[..8],
            userId,
            PermissionCodes.RolesManage);

        var act = () => _service.AuthorizeAsync(userId, PermissionCodes.RolesManage);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task UserWithoutAssignedPermissionIsDeniedAndAudited()
    {
        var userId = await InsertUserAsync();

        var act = () => _service.AuthorizeAsync(userId, PermissionCodes.RolesManage);

        await act.Should().ThrowAsync<AuthorizationDeniedException>()
            .Where(ex => ex.PermissionCode == PermissionCodes.RolesManage
                && ex.Reason == "No assigned role grants the permission");

        var count = await _database.ScalarAsync<long>(
            $"SELECT count(*) FROM identity.denial_events WHERE user_id = '{userId}';");
        count.Should().Be(1);
    }

    [Fact]
    public async Task InactiveUserIsDenied()
    {
        var userId = await InsertUserAsync(active: false);
        await _database.SeedRoleWithPermissionAsync(
            "cashier_" + Guid.NewGuid().ToString("N")[..8],
            userId,
            PermissionCodes.RolesManage);

        var act = () => _service.AuthorizeAsync(userId, PermissionCodes.RolesManage);

        await act.Should().ThrowAsync<AuthorizationDeniedException>()
            .Where(ex => ex.Reason == "User is inactive");
    }

    [Fact]
    public async Task UnknownUserIsDenied()
    {
        var act = () => _service.AuthorizeAsync(Guid.NewGuid(), PermissionCodes.RolesManage);

        await act.Should().ThrowAsync<AuthorizationDeniedException>()
            .Where(ex => ex.Reason == "User does not exist");
    }

    [Fact]
    public async Task UnknownPermissionCodeIsNeverGranted()
    {
        var userId = await InsertUserAsync();
        await _database.SeedRoleWithPermissionAsync(
            "cashier_" + Guid.NewGuid().ToString("N")[..8],
            userId,
            PermissionCodes.RolesManage);

        var act = () => _service.AuthorizeAsync(userId, "identity.something.else");

        await act.Should().ThrowAsync<AuthorizationDeniedException>();
    }
}