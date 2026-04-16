using Azure.Data.Tables;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Azure.Configuration;
using TestFramework.Azure.Configuration.SpecificConfigs;
using TestFramework.Azure.Identifier;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Logging;
using TestFramework.Core.Variables;

namespace TestFramework.Azure.StorageAccount.Table;

public class TableStorageEntityArtifactQueryFinder<T>(StorageAccountIdentifier identifier, VariableReference<string> tableName, string filter)
    : ArtifactFinder<TableStorageEntityArtifactDescriber<T>, TableStorageEntityArtifactData<T>, TableStorageEntityArtifactReference<T>>
    where T : class, ITableEntity
{
    public override async Task<ArtifactFinderResult?> FindAsync(IServiceProvider serviceProvider, VariableStore variableStore, ScopedLogger logger, CancellationToken cancellationToken)
    {
        StorageAccountConfig config = serviceProvider.GetRequiredService<ConfigStore<StorageAccountConfig>>().GetConfig(identifier);
        TableServiceClient serviceClient = new TableServiceClient(config.ConnectionString);
        TableClient tableClient = serviceClient.GetTableClient(tableName.GetRequiredValue(variableStore));

        await foreach (T entity in tableClient.QueryAsync<T>(filter, cancellationToken: cancellationToken))
        {
            return new ArtifactFinderResult(new TableStorageEntityArtifactReference<T>(identifier, tableName, entity.PartitionKey, entity.RowKey));
        }

        return null;
    }

    public override async Task<ArtifactFinderResultMulti> FindMultiAsync(IServiceProvider serviceProvider, VariableStore variableStore, ScopedLogger logger, CancellationToken cancellationToken)
    {
        StorageAccountConfig config = serviceProvider.GetRequiredService<ConfigStore<StorageAccountConfig>>().GetConfig(identifier);
        TableServiceClient serviceClient = new TableServiceClient(config.ConnectionString);
        TableClient tableClient = serviceClient.GetTableClient(tableName.GetRequiredValue(variableStore));

        List<ArtifactFinderResult> data = [];
        await foreach (T entity in tableClient.QueryAsync<T>(filter, cancellationToken: cancellationToken))
        {
            data.Add(new ArtifactFinderResult(new TableStorageEntityArtifactReference<T>(identifier, tableName, entity.PartitionKey, entity.RowKey)));
        }

        return new ArtifactFinderResultMulti([.. data]);
    }
}
