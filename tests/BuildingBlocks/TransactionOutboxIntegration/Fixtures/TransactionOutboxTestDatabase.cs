using Npgsql;
using Xunit;

namespace ALKAROS.TransactionOutboxIntegration.Tests.Fixtures;

/// <summary>
/// Creates and drops a unique PostgreSQL database for a single test class
/// against the local PostgreSQL 18 instance and applies the V1-FND-002
/// migration scripts. Connection defaults can be overridden with the
/// ALKAROS_TEST_PG_HOST / ALKAROS_TEST_PG_PORT / ALKAROS_TEST_PG_USER /
/// ALKAROS_TEST_PG_PASSWORD environment variables.
/// </summary>
public sealed class TransactionOutboxTestDatabase : IAsyncLifetime
{
    private const string DefaultHost = "localhost";
    private const int DefaultPort = 5432;
    private const string DefaultUser = "postgres";

    private readonly string _host;
    private readonly int _port;
    private readonly string _user;
    private readonly string? _password;
    private NpgsqlDataSource? _dataSource;

    public TransactionOutboxTestDatabase()
    {
        _host = Environment.GetEnvironmentVariable("ALKAROS_TEST_PG_HOST") ?? DefaultHost;
        _port = int.TryParse(Environment.GetEnvironmentVariable("ALKAROS_TEST_PG_PORT"), out var parsedPort)
            ? parsedPort
            : DefaultPort;
        _user = Environment.GetEnvironmentVariable("ALKAROS_TEST_PG_USER") ?? DefaultUser;
        _password = Environment.GetEnvironmentVariable("ALKAROS_TEST_PG_PASSWORD");
        Name = "alkaros_fnd006_" + Guid.NewGuid().ToString("N")[..8];
    }

    public string Name { get; }

    public NpgsqlDataSource DataSource
        => _dataSource ?? throw new InvalidOperationException("Test database is not initialized.");

    public async Task InitializeAsync()
    {
        await using var maintenance = CreateDataSource("postgres");
        await RunAsync(maintenance, $"DROP DATABASE IF EXISTS {Name} WITH (FORCE);");
        await RunAsync(maintenance, $"CREATE DATABASE {Name};");

        _dataSource = CreateDataSource(Name);
        var sqlDirectory = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql");
        foreach (var file in Directory.GetFiles(sqlDirectory, "*.up.sql").OrderBy(f => f))
            await RunAsync(_dataSource, await File.ReadAllTextAsync(file));
    }

    public async Task DisposeAsync()
    {
        if (_dataSource is not null)
        {
            await _dataSource.DisposeAsync();
            _dataSource = null;
        }

        await using var maintenance = CreateDataSource("postgres");
        await RunAsync(maintenance, $"DROP DATABASE IF EXISTS {Name} WITH (FORCE);");
    }

    public async Task<int> ExecuteAsync(string sql, params (string Name, object Value)[] parameters)
    {
        await using var command = DataSource.CreateCommand(sql);
        foreach (var parameter in parameters)
            command.Parameters.AddWithValue(parameter.Name, parameter.Value);
        return await command.ExecuteNonQueryAsync();
    }

    public async Task<T> ScalarAsync<T>(string sql)
    {
        await using var command = DataSource.CreateCommand(sql);
        var result = await command.ExecuteScalarAsync();
        Assert.NotNull(result);
        return (T)result;
    }

    public async Task<int> CountAsync(string table)
        => await ScalarAsync<long>($"SELECT count(*) FROM {table};") is { } count ? (int)count : 0;

    public async Task ResetTablesAsync()
        => await ExecuteAsync("TRUNCATE TABLE outbox_messages RESTART IDENTITY CASCADE;");

    private NpgsqlDataSource CreateDataSource(string database)
    {
        var connectionString = $"Host={_host};Port={_port};Username={_user};Database={database}"
            + (string.IsNullOrEmpty(_password) ? string.Empty : $";Password={_password}");
        return new NpgsqlDataSourceBuilder(connectionString).Build();
    }

    private static async Task RunAsync(NpgsqlDataSource dataSource, string sql)
    {
        await using var command = dataSource.CreateCommand(sql);
        await command.ExecuteNonQueryAsync();
    }
}
