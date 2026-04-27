using Azure;
using Azure.Data.Tables;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;
using TestFramework.Azure.Configuration;
using TestFramework.Azure.Configuration.SpecificConfigs;
using TestFramework.Azure.Identifier;
using TestFramework.Azure.Runtime;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Variables;

namespace TestFramework.Azure.StorageAccount.Table;

/// <summary>
/// Reference to an Azure Table entity addressed by table name, partition key, and row key.
/// </summary>
/// <typeparam name="T">The table entity type.</typeparam>
/// <param name="identifier">The Storage Account identifier.</param>
/// <param name="tableName">The table name variable.</param>
/// <param name="partitionKey">The partition key variable.</param>
/// <param name="rowKey">The row key variable.</param>
public class TableStorageEntityArtifactReference<T>(StorageAccountIdentifier identifier, VariableReference<string> tableName, VariableReference<string> partitionKey, VariableReference<string> rowKey)
    : ArtifactReference<TableStorageEntityArtifactReference<T>, TableStorageEntityArtifactDescriber<T>, TableStorageEntityArtifactData<T>>
    where T : class, ITableEntity
{
    private string _pinnedTable = "";
    private string _pinnedPartitionKey = "";
    private string _pinnedRowKey = "";

    /// <summary>
    /// The Storage Account identifier used to resolve the table.
    /// </summary>
    public StorageAccountIdentifier Identifier { get; } = identifier;

    /// <summary>
    /// Pins the reference to concrete table coordinates.
    /// </summary>
    public override void OnPinReference(VariableStore variableStore, ScopedLogger logger)
    {
        _pinnedTable = tableName.GetRequiredValue(variableStore);
        _pinnedPartitionKey = partitionKey.GetRequiredValue(variableStore);
        _pinnedRowKey = rowKey.GetRequiredValue(variableStore);
        CanDeconstruct = true;
    }

    /// <summary>
    /// Resolves the reference into concrete table entity artifact data.
    /// </summary>
    public override async Task<ArtifactResolveResult<TableStorageEntityArtifactDescriber<T>, TableStorageEntityArtifactData<T>, TableStorageEntityArtifactReference<T>>> ResolveToDataAsync(
        IServiceProvider serviceProvider, ArtifactVersionIdentifier versionIdentifier, VariableStore variableStore, ScopedLogger logger)
    {
        StorageAccountConfig config = serviceProvider.GetRequiredService<ConfigStore<StorageAccountConfig>>().GetConfig(Identifier);
        ITableAdapter tableClient = serviceProvider.GetAzureComponentFactory().Table.CreateTable(config, GetTableName(variableStore));
        TableReadResponse<T> response = await tableClient.GetEntityAsync<T>(GetPartitionKey(variableStore), GetRowKey(variableStore));
        if (response.Found)
        {
            return new ArtifactResolveResult<TableStorageEntityArtifactDescriber<T>, TableStorageEntityArtifactData<T>, TableStorageEntityArtifactReference<T>>
            {
                Found = true,
                Data = new TableStorageEntityArtifactData<T>(response.Entity ?? throw new InvalidOperationException("Found table response without entity.")) { Identifier = versionIdentifier }
            };
        }

        return new ArtifactResolveResult<TableStorageEntityArtifactDescriber<T>, TableStorageEntityArtifactData<T>, TableStorageEntityArtifactReference<T>>
        {
            Found = false
        };
    }

    /// <summary>
    /// Declares the variable inputs required by the reference.
    /// </summary>
    public override void DeclareIO(StepIOContract contract)
    {
        if (tableName.HasIdentifier)
            contract.Inputs.Add(new StepIOEntry(tableName.Identifier!.Identifier, StepIOKind.Variable, true, typeof(string)));
        if (partitionKey.HasIdentifier)
            contract.Inputs.Add(new StepIOEntry(partitionKey.Identifier!.Identifier, StepIOKind.Variable, true, typeof(string)));
        if (rowKey.HasIdentifier)
            contract.Inputs.Add(new StepIOEntry(rowKey.Identifier!.Identifier, StepIOKind.Variable, true, typeof(string)));
    }

            /// <summary>
            /// Gets the resolved table name.
            /// </summary>
            /// <param name="variableStore">The variable store used to resolve the value.</param>
            /// <returns>The table name.</returns>
    public string GetTableName(VariableStore variableStore) => IsPinned ? _pinnedTable : tableName.GetRequiredValue(variableStore);

            /// <summary>
            /// Gets the resolved partition key.
            /// </summary>
            /// <param name="variableStore">The variable store used to resolve the value.</param>
            /// <returns>The partition key.</returns>
    public string GetPartitionKey(VariableStore variableStore) => IsPinned ? _pinnedPartitionKey : partitionKey.GetRequiredValue(variableStore);

            /// <summary>
            /// Gets the resolved row key.
            /// </summary>
            /// <param name="variableStore">The variable store used to resolve the value.</param>
            /// <returns>The row key.</returns>
    public string GetRowKey(VariableStore variableStore) => IsPinned ? _pinnedRowKey : rowKey.GetRequiredValue(variableStore);

            /// <summary>
            /// Returns a readable string representation of the reference.
            /// </summary>
            /// <returns>A string representation of the reference.</returns>
    public override string ToString() => IsPinned
        ? $"Azure Table<{typeof(T).Name}>: {_pinnedTable}/{_pinnedPartitionKey}/{_pinnedRowKey}"
        : $"Azure Table<{typeof(T).Name}> (unresolved)";
}
