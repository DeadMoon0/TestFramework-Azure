using Azure.Data.Tables;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;
using TestFramework.Azure.Configuration;
using TestFramework.Azure.Configuration.SpecificConfigs;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Logging;
using TestFramework.Core.Variables;

namespace TestFramework.Azure.StorageAccount.Table;

public class TableStorageEntityArtifactDescriber<T> : ArtifactDescriber<TableStorageEntityArtifactDescriber<T>, TableStorageEntityArtifactData<T>, TableStorageEntityArtifactReference<T>>
    where T : class, ITableEntity
{
    public override async Task Setup(IServiceProvider serviceProvider, TableStorageEntityArtifactData<T> data, TableStorageEntityArtifactReference<T> reference, VariableStore variableStore, ScopedLogger logger)
    {
        StorageAccountConfig config = serviceProvider.GetRequiredService<ConfigStore<StorageAccountConfig>>().GetConfig(reference.Identifier);
        TableServiceClient serviceClient = new TableServiceClient(config.ConnectionString);
        TableClient tableClient = serviceClient.GetTableClient(reference.GetTableName(variableStore));
        await tableClient.CreateIfNotExistsAsync();
        await tableClient.UpsertEntityAsync(data.Entity);

        logger.LogInformation($"Table entity {reference.GetPartitionKey(variableStore)}/{reference.GetRowKey(variableStore)} upserted.");
    }

    public override async Task Deconstruct(IServiceProvider serviceProvider, TableStorageEntityArtifactReference<T> reference, VariableStore variableStore, ScopedLogger logger)
    {
        StorageAccountConfig config = serviceProvider.GetRequiredService<ConfigStore<StorageAccountConfig>>().GetConfig(reference.Identifier);
        TableServiceClient serviceClient = new TableServiceClient(config.ConnectionString);
        TableClient tableClient = serviceClient.GetTableClient(reference.GetTableName(variableStore));
        await tableClient.DeleteEntityAsync(reference.GetPartitionKey(variableStore), reference.GetRowKey(variableStore));

        logger.LogInformation($"Table entity {reference.GetPartitionKey(variableStore)}/{reference.GetRowKey(variableStore)} deleted.");
    }

    public override string ToString() => $"Azure Table Entity<{typeof(T).Name}>";
}
