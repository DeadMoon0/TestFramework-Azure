using Azure.Data.Tables;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;
using TestFramework.Azure.Configuration;
using TestFramework.Azure.Configuration.SpecificConfigs;
using TestFramework.Azure.Runtime;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Logging;
using TestFramework.Core.Variables;

namespace TestFramework.Azure.StorageAccount.Table;

/// <summary>
/// Sets up and tears down Azure Table entity artifacts.
/// </summary>
/// <typeparam name="T">The table entity type.</typeparam>
public class TableStorageEntityArtifactDescriber<T> : ArtifactDescriber<TableStorageEntityArtifactDescriber<T>, TableStorageEntityArtifactData<T>, TableStorageEntityArtifactReference<T>>
    where T : class, ITableEntity
{
    /// <summary>
    /// Upserts the referenced entity during artifact setup.
    /// </summary>
    public override async Task Setup(IServiceProvider serviceProvider, TableStorageEntityArtifactData<T> data, TableStorageEntityArtifactReference<T> reference, VariableStore variableStore, ScopedLogger logger)
    {
        StorageAccountConfig config = serviceProvider.GetRequiredService<ConfigStore<StorageAccountConfig>>().GetConfig(reference.Identifier);
        ITableAdapter tableClient = serviceProvider.GetAzureComponentFactory().Table.CreateTable(config, reference.GetTableName(variableStore));
        await tableClient.CreateIfNotExistsAsync();
        await tableClient.UpsertEntityAsync(data.Entity);

        logger.LogInformation($"Table entity {reference.GetPartitionKey(variableStore)}/{reference.GetRowKey(variableStore)} upserted.");
    }

    /// <summary>
    /// Deletes the referenced entity during artifact cleanup.
    /// </summary>
    public override async Task Deconstruct(IServiceProvider serviceProvider, TableStorageEntityArtifactReference<T> reference, VariableStore variableStore, ScopedLogger logger)
    {
        StorageAccountConfig config = serviceProvider.GetRequiredService<ConfigStore<StorageAccountConfig>>().GetConfig(reference.Identifier);
        ITableAdapter tableClient = serviceProvider.GetAzureComponentFactory().Table.CreateTable(config, reference.GetTableName(variableStore));
        await tableClient.DeleteEntityAsync(reference.GetPartitionKey(variableStore), reference.GetRowKey(variableStore));

        logger.LogInformation($"Table entity {reference.GetPartitionKey(variableStore)}/{reference.GetRowKey(variableStore)} deleted.");
    }

    /// <summary>
    /// Returns a readable string representation of the describer.
    /// </summary>
    /// <returns>A string representation of the describer.</returns>
    public override string ToString() => $"Azure Table Entity<{typeof(T).Name}>";
}
