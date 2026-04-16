using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using TestFramework.Azure.Identifier;
using TestFramework.Core.Logging;

namespace TestFramework.Azure.DB.SqlServer;

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
    DbContext Resolve(SqlDatabaseIdentifier identifier);

    /// <summary>
    /// Validates (and optionally applies) migrations for the given context.
    /// Must be called before every database operation.
    /// </summary>
    Task EnsureReadyAsync(DbContext context, SqlDatabaseIdentifier identifier, ScopedLogger logger);
}
