using TestFramework.Core.Steps;
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
public class TableStorageEntityArtifactReference<T>
    : ArtifactReference<TableStorageEntityArtifactReference<T>, TableStorageEntityArtifactDescriber<T>, TableStorageEntityArtifactData<T>>
    where T : class, ITableEntity
{
    private readonly VariableReference<string> _tableName;
    private readonly VariableReference<string> _partitionKey;
    private readonly VariableReference<string> _rowKey;

    private string _pinnedTable = "";
    private string _pinnedPartitionKey = "";
    private string _pinnedRowKey = "";

    /// <summary>
    /// Initializes a reference to one table entity.
    /// </summary>
    /// <param name="identifier">The Storage Account identifier.</param>
    /// <param name="tableName">The table name variable.</param>
    /// <param name="partitionKey">The partition key variable.</param>
    /// <param name="rowKey">The row key variable.</param>
    public TableStorageEntityArtifactReference(StorageAccountIdentifier identifier, VariableReference<string> tableName, VariableReference<string> partitionKey, VariableReference<string> rowKey)
    {
        Identifier = identifier;
        _tableName = tableName;
        _partitionKey = partitionKey;
        _rowKey = rowKey;

        // All three coordinates are required, so this reference can always address the entity it
        // deletes. Whether it may delete it is the timeline's call, through MarkReadonly().
        CanDeconstruct = true;
    }

    /// <summary>
    /// The Storage Account identifier used to resolve the table.
    /// </summary>
    public StorageAccountIdentifier Identifier { get; }

    /// <summary>
    /// Pins the reference to concrete table coordinates.
    /// </summary>
    public override void OnPinReference(RunContext context)
    {
        _pinnedTable = _tableName.GetRequiredValue(context.Variables);
        _pinnedPartitionKey = _partitionKey.GetRequiredValue(context.Variables);
        _pinnedRowKey = _rowKey.GetRequiredValue(context.Variables);
    }

    /// <summary>
    /// Resolves the reference into concrete table entity artifact data.
    /// </summary>
    public override async Task<ArtifactResolveResult<TableStorageEntityArtifactDescriber<T>, TableStorageEntityArtifactData<T>, TableStorageEntityArtifactReference<T>>> ResolveToDataAsync(RunContext context, ArtifactVersionIdentifier versionIdentifier)
    {
        ITableAdapter tableClient = context.Services.GetAzureComponentFactory()
            .Table.CreateTable(AzureResources.For<StorageAccountConfig>(context, Identifier), GetTableName(context.Variables));
        TableReadResponse<T> response = await tableClient.GetEntityAsync<T>(GetPartitionKey(context.Variables), GetRowKey(context.Variables));
        if (response.Found)
        {
            return new ArtifactResolveResult<TableStorageEntityArtifactDescriber<T>, TableStorageEntityArtifactData<T>, TableStorageEntityArtifactReference<T>>
            {
                Found = true,
                Data = new TableStorageEntityArtifactData<T>(response.Entity ?? throw new TestFramework.Core.Exceptions.FrameworkStateException("Found table response without entity.")) { Identifier = versionIdentifier }
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
        if (_tableName.HasIdentifier)
            contract.Inputs.Add(new StepIOEntry(_tableName.Identifier!.Identifier, StepIOKind.Variable, true, typeof(string)));
        if (_partitionKey.HasIdentifier)
            contract.Inputs.Add(new StepIOEntry(_partitionKey.Identifier!.Identifier, StepIOKind.Variable, true, typeof(string)));
        if (_rowKey.HasIdentifier)
            contract.Inputs.Add(new StepIOEntry(_rowKey.Identifier!.Identifier, StepIOKind.Variable, true, typeof(string)));
    }

            /// <summary>
            /// Gets the resolved table name.
            /// </summary>
            /// <returns>The table name.</returns>
    public string GetTableName(VariableStore variableStore) => IsPinned ? _pinnedTable : _tableName.GetRequiredValue(variableStore);

            /// <summary>
            /// Gets the resolved partition key.
            /// </summary>
            /// <returns>The partition key.</returns>
    public string GetPartitionKey(VariableStore variableStore) => IsPinned ? _pinnedPartitionKey : _partitionKey.GetRequiredValue(variableStore);

            /// <summary>
            /// Gets the resolved row key.
            /// </summary>
            /// <returns>The row key.</returns>
    public string GetRowKey(VariableStore variableStore) => IsPinned ? _pinnedRowKey : _rowKey.GetRequiredValue(variableStore);

            /// <summary>
            /// Returns a readable string representation of the reference.
            /// </summary>
            /// <returns>A string representation of the reference.</returns>
    public override string ToString() => IsPinned
        ? $"Azure Table<{typeof(T).Name}>: {_pinnedTable}/{_pinnedPartitionKey}/{_pinnedRowKey}"
        : $"Azure Table<{typeof(T).Name}> (unresolved)";
}
