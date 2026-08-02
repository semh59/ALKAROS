using Npgsql;
using Xunit;

namespace ALKAROS.TestHelpers;

/// <summary>
/// Base class for PostgreSQL test database fixtures. Creates and drops a
/// unique database per test class and exposes <see cref="DataSource"/>,
/// <see cref="ExecuteAsync"/>, <see cref="ScalarAsync{T}"/> and
/// <see cref="CountAsync"/> helpers. Subclasses provide the database name
/// prefix and apply migration scripts in <see cref="ApplySqlAsync"/>.
/// </summary>
public abstract class PgTestDatabase : IAsyncLifetime
{
    private const string DefaultHost = "localhost";
    private const int DefaultPort = 5432;
    private const string DefaultUser = "postgres";

    private readonly string _host;
    private readonly int _port;
    private readonly string _user;
    private readonly string? _password;
    private NpgsqlDataSource? _dataSource;

    protected PgTestDatabase(string prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix))
            throw new ArgumentException("Prefix must be non-empty.", nameof(prefix));

        _host = Environment.GetEnvironmentVariable("ALKAROS_TEST_PG_HOST") ?? DefaultHost;
        _port = int.TryParse(Environment.GetEnvironmentVariable("ALKAROS_TEST_PG_PORT"), out var parsedPort)
            ? parsedPort
            : DefaultPort;
        _user = Environment.GetEnvironmentVariable("ALKAROS_TEST_PG_USER") ?? DefaultUser;
        _password = Environment.GetEnvironmentVariable("ALKAROS_TEST_PG_PASSWORD");
        Name = prefix + Guid.NewGuid().ToString("N")[..8];
    }

    /// <summary>
    /// The unique database name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// An open data source connected to the test database. Available after
    /// <see cref="InitializeAsync"/> completes.
    /// </summary>
    public NpgsqlDataSource DataSource
        => _dataSource ?? throw new InvalidOperationException("Test database is not initialized.");

    /// <summary>
    /// Applies migration scripts after the database is created. Called by
    /// <see cref="InitializeAsync"/>.
    /// </summary>
    protected abstract Task ApplySqlAsync();

    /// <summary>
    /// Executes a SQL statement against the test database.
    /// </summary>
    protected static async Task RunAsync(NpgsqlDataSource dataSource, string sql)
    {
        await using var command = dataSource.CreateCommand(sql);
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Creates the database and applies migrations.
    /// </summary>
    public async Task InitializeAsync()
    {
        await using var maintenance = CreateDataSource("postgres");
        await RunAsync(maintenance, $"DROP DATABASE IF EXISTS {Name} WITH (FORCE);");
        await RunAsync(maintenance, $"CREATE DATABASE {Name};");

        _dataSource = CreateDataSource(Name);
        await ApplySqlAsync();
    }

    /// <summary>
    /// Drops the database.
    /// </summary>
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

    /// <summary>
    /// Executes a parameterised SQL command and returns the row count.
    /// </summary>
    public async Task<int> ExecuteAsync(string sql, params (string Name, object Value)[] parameters)
    {
        await using var command = DataSource.CreateCommand(sql);
        foreach (var parameter in parameters)
            command.Parameters.AddWithValue(parameter.Name, parameter.Value);
        return await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Executes a scalar SQL command and returns the result cast to <typeparamref name="T"/>.
    /// </summary>
    public async Task<T> ScalarAsync<T>(string sql)
    {
        await using var command = DataSource.CreateCommand(sql);
        var result = await command.ExecuteScalarAsync();
        Assert.NotNull(result);
        return (T)result;
    }

    /// <summary>
    /// Returns the number of rows in <paramref name="table"/>.
    /// </summary>
    public async Task<int> CountAsync(string table)
        => await ScalarAsync<long>($"SELECT count(*) FROM {table};") is { } count ? (int)count : 0;

    private NpgsqlDataSource CreateDataSource(string database)
    {
        var connectionString = $"Host={_host};Port={_port};Username={_user};Database={database}"
            + (string.IsNullOrEmpty(_password) ? string.Empty : $";Password={_password}");
        return new NpgsqlDataSourceBuilder(connectionString).Build();
    }
}