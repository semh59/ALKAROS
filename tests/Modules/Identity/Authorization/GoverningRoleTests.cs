using ALKAROS.Identity.Authorization.Tests.Fixtures;
using FluentAssertions;
using Xunit;

namespace ALKAROS.Identity.Authorization.Tests;

/// <summary>
/// V1-RMD-408 (PO decision 2026-09-28): a multi-role user's grant requests are governed by their most restrictive
/// role — the one holding the fewest permissions outright, ties broken by the ordinally smallest role code.
/// </summary>
public sealed class GoverningRoleTests : IClassFixture<AuthorizationTestDatabase>
{
    private readonly AuthorizationTestDatabase _database;
    private readonly PostgresRoleRepository _roles;

    public GoverningRoleTests(AuthorizationTestDatabase database)
    {
        _database = database;
        _roles = new PostgresRoleRepository(database.DataSource);
    }

    [Fact]
    public async Task TheRoleWithFewerPermissionsGovernsRegardlessOfAssignmentOrder()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var userId = await _database.InsertUserAsync("rmd408-multi-" + suffix);
        await _database.SeedRoleWithPermissionAsync(
            "rmd408-broad-" + suffix, userId, "identity.users.manage", "identity.roles.manage");
        await _database.SeedRoleWithPermissionAsync("rmd408-narrow-" + suffix, userId, "identity.users.manage");

        var governing = await _roles.GetGoverningRoleForUserAsync(userId);

        governing!.Code.Should().Be("rmd408-narrow-" + suffix);
    }

    [Fact]
    public async Task ATieGoesToTheOrdinallySmallestRoleCode()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var userId = await _database.InsertUserAsync("rmd408-tie-" + suffix);
        await _database.SeedRoleWithPermissionAsync("rmd408-b-" + suffix, userId, "identity.users.manage");
        await _database.SeedRoleWithPermissionAsync("rmd408-a-" + suffix, userId, "identity.roles.manage");

        var governing = await _roles.GetGoverningRoleForUserAsync(userId);

        governing!.Code.Should().Be("rmd408-a-" + suffix);
    }

    [Fact]
    public async Task AUserWithoutARoleHasNoGoverningRole()
    {
        var userId = await _database.InsertUserAsync("rmd408-none-" + Guid.NewGuid().ToString("N")[..8]);

        (await _roles.GetGoverningRoleForUserAsync(userId)).Should().BeNull();
    }
}
