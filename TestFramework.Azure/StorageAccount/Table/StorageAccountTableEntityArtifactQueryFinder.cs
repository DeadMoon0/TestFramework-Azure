using TestFramework.Core.Steps;
using Azure.Data.Tables;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Azure.Configuration;
using TestFramework.Azure.Configuration.SpecificConfigs;
using TestFramework.Azure.Identifier;
using TestFramework.Azure.Runtime;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Logging;
using TestFramework.Core.Variables;

namespace TestFramework.Azure.StorageAccount.Table;

/// <summary>
/// Finds Azure Table entity artifacts by executing a table query.
/// </summary>
/// <typeparam name="T">The table entity type returned by the query.</typeparam>
/// <param name="identifier">The Storage Account identifier.</param>
/// <param name="tableName">The table name variable.</param>
/// <param name="filter">The Azure Table filter expression.</param>
public class StorageAccountTableEntityArtifactQueryFinder<T>(StorageAccountIdentifier identifier, VariableReference<string> tableName, string filter)
    : ArtifactFinder<StorageAccountTableEntityArtifactDescriber<T>, StorageAccountTableEntityArtifactData<T>, StorageAccountTableEntityArtifactReference<T>>
    where T : class, ITableEntity
{
    /// <summary>
    /// Finds the first matching table entity.
    /// </summary>
    public override async Task<ArtifactFinderResult?> FindAsync(RunContext context)
    {
        ITableAdapter tableClient = context.Services.GetAzureComponentFactory()
            .Table.CreateTable(AzureResources.For<StorageAccountConfig>(context, identifier), tableName.GetRequiredValue(context.Variables));

        await foreach (T entity in tableClient.QueryEntitiesAsync<T>(filter, context.Deadline.Token))
        {
            return new ArtifactFinderResult(new StorageAccountTableEntityArtifactReference<T>(identifier, tableName, entity.PartitionKey, entity.RowKey));
        }

        return null;
    }

    /// <summary>
    /// Finds all matching table entities.
    /// </summary>
    public override async Task<ArtifactFinderResultMulti> FindMultiAsync(RunContext context)
    {
        ITableAdapter tableClient = context.Services.GetAzureComponentFactory()
            .Table.CreateTable(AzureResources.For<StorageAccountConfig>(context, identifier), tableName.GetRequiredValue(context.Variables));

        List<ArtifactFinderResult> data = [];
        await foreach (T entity in tableClient.QueryEntitiesAsync<T>(filter, context.Deadline.Token))
        {
            data.Add(new ArtifactFinderResult(new StorageAccountTableEntityArtifactReference<T>(identifier, tableName, entity.PartitionKey, entity.RowKey)));
        }

        return new ArtifactFinderResultMulti([.. data]);
    }
}
