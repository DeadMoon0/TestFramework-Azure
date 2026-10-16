using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;
using TestFramework.Azure.Identifier;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps;

namespace TestFramework.Azure.DB.SqlDatabase;

internal class SqlDbContextResolver(SqlDbContextRegistry registry, SqlMigrationTracker migrationTracker) : ISqlDbContextResolver
{
    public DbContext Resolve(RunContext context, SqlDatabaseIdentifier identifier)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(identifier);

        if (registry.TryCreateByIdentifier(context, identifier, out DbContext? byIdentifier) && byIdentifier is not null)
            return byIdentifier;

        if (registry.TryCreateDefault(context, identifier, out DbContext? byDefault) && byDefault is not null)
            return byDefault;

        throw new TestFramework.Core.Exceptions.FrameworkConfigurationException(
            $"No DbContext is registered for SqlDatabase identifier '{identifier}'.")
        {
            RecoverySteps = [
                $"Register one: AddSqlArtifactContexts(reg => reg.AddForIdentifier<MyDbContext>(\"{identifier}\", options => new MyDbContext(options)))",
                "Or register a fallback for every identifier: reg.AddDefault<MyDbContext>(options => new MyDbContext(options))",
            ],
            AvailableOptions = [.. registry.RegisteredIdentifiers],
        };
    }

    public Task EnsureReadyAsync(RunContext context, DbContext dbContext, SqlDatabaseIdentifier identifier)
    {
        ArgumentNullException.ThrowIfNull(context);

        return migrationTracker.EnsureReadyAsync(dbContext, identifier, context.Logger, context.Deadline.Token);
    }
}
