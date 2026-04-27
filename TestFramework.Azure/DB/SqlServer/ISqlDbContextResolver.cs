using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using TestFramework.Azure.Identifier;
using TestFramework.Core.Logging;

namespace TestFramework.Azure.DB.SqlServer;

/// <summary>
/// Resolves EF Core <c>DbContext</c> instances for SQL-backed Azure artifacts and triggers.
/// </summary>
public interface ISqlDbContextResolver
{
    /// <summary>
    /// Resolves a DbContext for the given SQL database identifier.
    /// Resolution order:
    ///   1. Identifier-based registration (AddForIdentifier)
    ///   2. ContextType in config (CLR type resolved from DI)
    ///   3. Default registration (AddDefault)
    /// Throws if no context can be resolved.
    /// </summary>
    /// <param name="identifier">The SQL database identifier requested by the DSL.</param>
    /// <returns>The resolved <c>DbContext</c>.</returns>
    DbContext Resolve(SqlDatabaseIdentifier identifier);

    /// <summary>
    /// Validates (and optionally applies) migrations for the given context.
    /// Must be called before every database operation.
    /// </summary>
    /// <param name="context">The resolved context instance.</param>
    /// <param name="identifier">The SQL database identifier requested by the DSL.</param>
    /// <param name="logger">The logger used to report migration actions.</param>
    /// <returns>A task that completes when the context is ready for use.</returns>
    Task EnsureReadyAsync(DbContext context, SqlDatabaseIdentifier identifier, ScopedLogger logger);
}
