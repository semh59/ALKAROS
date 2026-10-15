using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ALKAROS.Host.Observability.Wiring;

/// <summary>
/// The monitoring system's own small database pool, kept apart from the application pool so an event
/// storm can never take connections from orders and payments.
/// </summary>
public sealed class MonitoringDataSource(NpgsqlDataSource inner)
{
    public const int MaxConnections = 4;
    public const string ApplicationName = "alkaros-monitoring";

    public NpgsqlDataSource Inner { get; } = inner;

    public static NpgsqlDataSource Create(string connectionString)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString)
        {
            MaxPoolSize = MaxConnections,
            MinPoolSize = 0,
            ApplicationName = ApplicationName,
        };
        return NpgsqlDataSource.Create(builder.ConnectionString);
    }
}

public static class MonitoringWiringExtensions
{
    public static IServiceCollection AddMonitoringDataSource(this IServiceCollection services, string connectionString)
    {
        services.AddSingleton(new MonitoringDataSource(MonitoringDataSource.Create(connectionString)));
        return services;
    }
}
