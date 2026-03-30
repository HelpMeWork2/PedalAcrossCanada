using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PedalAcrossCanada.Server.Infrastructure.Data;

namespace PedalAcrossCanada.Server.Tests.Helpers;

/// <summary>
/// Creates a fresh SQLite in-memory <see cref="AppDbContext"/> per test.
/// Dispose the returned instance to close the connection.
/// </summary>
public sealed class TestDbContextFactory : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public TestDbContextFactory()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    public AppDbContext CreateContext() => new(_options);

    public void Dispose()
    {
        _connection.Dispose();
    }
}
