using Npgsql;
using Testcontainers.PostgreSql;

namespace ReturnRight.IntegrationTests;

/// <summary>
/// Provides a Postgres connection string for the test run.
/// Uses RETURNRIGHT_TEST_CONNECTION (an admin-level connection to a running
/// server) when set — creates a fresh rr_test_&lt;guid&gt; database and drops it
/// afterwards. Otherwise starts a Testcontainers postgres:17-alpine container.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _container;
    private string? _adminConnectionString;
    private string? _ownedDatabase;

    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        var external = Environment.GetEnvironmentVariable("RETURNRIGHT_TEST_CONNECTION");
        if (string.IsNullOrEmpty(external))
        {
            _container = new PostgreSqlBuilder("postgres:17-alpine").Build();
            await _container.StartAsync();
            ConnectionString = _container.GetConnectionString();
            return;
        }

        _adminConnectionString = external;
        _ownedDatabase = $"rr_test_{Guid.NewGuid():N}";

        await using (var connection = new NpgsqlConnection(external))
        {
            await connection.OpenAsync();
            await using var create = connection.CreateCommand();
            create.CommandText = $"CREATE DATABASE \"{_ownedDatabase}\"";
            await create.ExecuteNonQueryAsync();
        }

        var builder = new NpgsqlConnectionStringBuilder(external) { Database = _ownedDatabase };
        ConnectionString = builder.ConnectionString;
    }

    public async Task DisposeAsync()
    {
        if (_ownedDatabase is not null && _adminConnectionString is not null)
        {
            await using var connection = new NpgsqlConnection(_adminConnectionString);
            await connection.OpenAsync();
            await using var drop = connection.CreateCommand();
            drop.CommandText = $"DROP DATABASE IF EXISTS \"{_ownedDatabase}\" WITH (FORCE)";
            await drop.ExecuteNonQueryAsync();
        }

        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }
}
