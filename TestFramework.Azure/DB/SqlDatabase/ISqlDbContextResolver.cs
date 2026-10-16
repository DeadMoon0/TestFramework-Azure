using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using TestFramework.Azure.Identifier;
using TestFramework.Core.Steps;

namespace TestFramework.Azure.DB.SqlDatabase;

/// <summary>
/// Builds EF Core <c>DbContext</c> instances for SQL-backed Azure artifacts and triggers.
/// </summary>
public interface ISqlDbContextResolver
{
    /// <summary>
    /// Builds a context for a SQL database identifier, against the database this run is using.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two roads, in order: a registration for this identifier, then a default registration covering every
    /// identifier. Neither is a lookup in a container - the registry constructs the context from options the
    /// framework points at the run's database, which is why this takes the run.
    /// </para>
    /// <para>
    /// There used to be a third road, a <c>ContextType</c> string in configuration resolved from DI. It is
    /// gone: a context resolved from a container takes its connection string from wherever that registration
    /// read it, which for a containerised database is a placeholder, and it was a third way of saying what
    /// the two registrations already say.
    /// </para>
    /// </remarks>
    /// <param name="context">The run.</param>
    /// <param name="identifier">The SQL database identifier requested by the DSL.</param>
    /// <returns>The context.</returns>
    DbContext Resolve(RunContext context, SqlDatabaseIdentifier identifier);

    /// <summary>
    /// Validates (and optionally applies) migrations for the given context.
    /// Must be called before every database operation.
    /// </summary>
    /// <remarks>
    /// Takes the run, as <see cref="Resolve"/> does: a migration is the longest thing a step can do to a database,
    /// and with only a logger it ran without the step's deadline, so a run past its budget could not stop it.
    /// </remarks>
    /// <param name="context">The run, whose deadline bounds the migration and whose logger reports it.</param>
    /// <param name="dbContext">The context instance.</param>
    /// <param name="identifier">The SQL database identifier requested by the DSL.</param>
    /// <returns>A task that completes when the context is ready for use.</returns>
    Task EnsureReadyAsync(RunContext context, DbContext dbContext, SqlDatabaseIdentifier identifier);
}
