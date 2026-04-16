using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Azure.Identifier;
using TestFramework.Core.Logging;

namespace TestFramework.Azure.DB.SqlServer;

/// <summary>
/// Singleton service that tracks per-database migration state.
/// State is static (process-wide) so concurrent test classes sharing the same
/// physical database only apply/validate migrations once, even when each test
/// has its own DI container.
/// </summary>
internal class SqlMigrationTracker
{
    private readonly bool _applyOnFirstUse;

    // Keyed by (connectionString + "|" + identifier) — static so all DI scopes share it.
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();
    private static readonly ConcurrentDictionary<string, bool> _completed = new();

    internal SqlMigrationTracker(bool applyOnFirstUse)
    {
        _applyOnFirstUse = applyOnFirstUse;
    }

    /// <summary>
    /// Ensures the database backing <paramref name="context"/> is migration-ready:
    /// <list type="bullet">
    ///   <item>First call per physical database (process-wide): optionally applies all pending
    ///         migrations, or calls EnsureCreated when no migrations are defined.</item>
    ///   <item>Every call: validates that no migrations are pending.</item>
    /// </list>
    /// </summary>
    internal async Task EnsureReadyAsync(DbContext context, SqlDatabaseIdentifier identifier, ScopedLogger logger)
    {
        string connectionString = context.Database.GetConnectionString() ?? identifier.Identifier;
        string key = $"{connectionString}|{identifier.Identifier}";

        // Fast path: already initialized for this database in this process.
        if (_completed.ContainsKey(key))
            return;

        SemaphoreSlim semaphore = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync();
        try
        {
            // Double-check inside the lock.
            if (_completed.ContainsKey(key))
                return;

            var allMigrations = context.Database.GetMigrations().ToList();

            if (_applyOnFirstUse)
            {
                if (allMigrations.Count == 0)
                {
                    logger.LogInformation($"[SQL Migration] No migrations defined for '{identifier}' — calling EnsureCreated.");
                    bool created = await context.Database.EnsureCreatedAsync();
                    if (!created)
                    {
                        // DB already exists (e.g. shared with another DbContext).
                        // Create any model tables that are missing, ignoring tables that already exist.
                        var creator = context.Database.GetService<IRelationalDatabaseCreator>();
                        try { await creator.CreateTablesAsync(); }
                        catch (Exception ex) when (IsTableAlreadyExistsException(ex))
                        {
                            // SQL Server error 2714: one or more tables already exist — expected when
                            // multiple DbContexts share the same database. All other exceptions propagate.
                            logger.LogInformation($"[SQL Migration] Some tables already exist for '{identifier}' — continuing.");
                        }
                    }
                }
                else
                {
                    logger.LogInformation($"[SQL Migration] Applying pending migrations for '{identifier}'...");
                    await context.Database.MigrateAsync();
                    logger.LogInformation($"[SQL Migration] Migrations applied for '{identifier}'.");
                }
            }

            // Validate: no pending migrations must remain.
            // Contexts with no migrations defined return empty — OK.
            if (allMigrations.Count > 0)
            {
                IReadOnlyList<string> pending = (await context.Database.GetPendingMigrationsAsync()).ToList();
                if (pending.Count > 0)
                {
                    throw new InvalidOperationException(
                        $"Database '{identifier}' has {pending.Count} unapplied migration(s): " +
                        string.Join(", ", pending) + ". " +
                        "Run 'dotnet ef database update' or call reg.ApplyMigrationsOnFirstUse() in AddSqlArtifactContexts.");
                }
            }

            _completed.TryAdd(key, true);
        }
        finally
        {
            semaphore.Release();
        }
    }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="ex"/> (or any inner exception) indicates
    /// that one or more database objects already exist — SQL Server error 2714.
    /// Checked without a hard reference to <c>Microsoft.Data.SqlClient</c> by inspecting the
    /// exception type name and its <c>Number</c> property via reflection.
    /// </summary>
    private static bool IsTableAlreadyExistsException(Exception ex)
    {
        for (Exception? current = ex; current is not null; current = current.InnerException)
        {
            if (string.Equals(current.GetType().Name, "SqlException", StringComparison.Ordinal))
            {
                object? number = current.GetType().GetProperty("Number")?.GetValue(current);
                if (number is int n && n == 2714)
                    return true;
            }
        }
        return false;
    }
}
