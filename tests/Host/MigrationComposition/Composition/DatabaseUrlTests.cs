using ALKAROS.Host.Composition;
using ALKAROS.Host.Composition.Migrations;
using Npgsql;
using Xunit;

namespace ALKAROS.Host.Tests.Composition;

public sealed class DatabaseUrlTests
{
    [Fact]
    public void AnEscapedUserNameAndDatabaseNameAreReadBackAsTheyWereWritten()
    {
        using var source = HostComposition.TryCreateDataSource(
            new PsqlOptions("postgresql://app%40team:ignored@db.local:6543/shop%20db", Password: "secret"));

        var builder = new NpgsqlConnectionStringBuilder(source!.ConnectionString);
        Assert.Equal("app@team", builder.Username);
        Assert.Equal("shop db", builder.Database);
        Assert.Equal("db.local", builder.Host);
        Assert.Equal(6543, builder.Port);
    }

    [Fact]
    public void AUrlWithoutUserOrDatabaseFallsBackToTheDefaults()
    {
        using var source = HostComposition.TryCreateDataSource(new PsqlOptions("postgresql://db.local", Password: "secret"));

        var builder = new NpgsqlConnectionStringBuilder(source!.ConnectionString);
        Assert.Equal("postgres", builder.Username);
        Assert.Equal("alkaros", builder.Database);
        Assert.Equal(5432, builder.Port);
    }
}

public sealed class StartupRequirementTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AMissingKitchenStationStopsTheServerAtStartup(string? stationId)
    {
        var exception = Assert.Throws<ALKAROS.Host.DualScreen.DualScreenStartupException>(
            () => ALKAROS.Host.DualScreen.DualScreenApplication.RequireKitchenStation(stationId));

        Assert.Contains("ALKAROS_KITCHEN_STATION_ID", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AConfiguredKitchenStationPasses()
        => ALKAROS.Host.DualScreen.DualScreenApplication.RequireKitchenStation("kitchen-main");
}

public sealed class OrderNumberTests
{
    [Fact]
    public void TheOrderNumberShowsTheRestaurantsLocalDateAndTime()
    {
        var number = ALKAROS.Host.DualScreen.DualScreenStore.OrderNumber(
            new DateTimeOffset(2026, 10, 23, 22, 30, 5, TimeSpan.Zero), Guid.Parse("0123456789abcdef0123456789abcdef"));

        Assert.StartsWith("POS-20261024-013005-", number, StringComparison.Ordinal);
        Assert.Equal(32, number.Length);
    }
}
