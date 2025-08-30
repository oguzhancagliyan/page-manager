using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PageManager.Infrastructure.Persistence;

namespace PageManager.UnitTests.Common;

public static class DbHelper
{
    public static SqliteConnection OpenConnection()
    {
        var conn = new SqliteConnection("Filename=:memory:");
        conn.Open();
        return conn;
    }

    public static DbContextOptions<AppDbContext> Options(SqliteConnection conn) =>
        new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(conn)
            .EnableSensitiveDataLogging()
            .Options;

    public static void EnsureCreated(SqliteConnection conn)
    {
        using var ctx = new AppDbContext(Options(conn));
        ctx.Database.EnsureCreated();
    }
}

public sealed class ThrowOnceConcurrencyContext : AppDbContext
{
    private bool _throwOnce;
    public ThrowOnceConcurrencyContext(DbContextOptions<AppDbContext> options) : base(options) => _throwOnce = true;

    public override Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        if (_throwOnce) { _throwOnce = false; throw new DbUpdateConcurrencyException("Simulated concurrency"); }
        return base.SaveChangesAsync(ct);
    }
}

public sealed class AlwaysFailConcurrencyContext : AppDbContext
{
    public AlwaysFailConcurrencyContext(DbContextOptions<AppDbContext> options) : base(options) { }
    public override Task<int> SaveChangesAsync(CancellationToken ct = default)
        => throw new DbUpdateConcurrencyException("Always failing");
}