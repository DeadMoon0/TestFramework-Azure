using Azure;
using Azure.Data.Tables;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;
using TestFramework.Azure.Configuration;
using TestFramework.Azure.Configuration.SpecificConfigs;
using TestFramework.Azure.Identifier;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Variables;

namespace TestFramework.Azure.StorageAccount.Table;

public class TableStorageEntityArtifactReference<T>(StorageAccountIdentifier identifier, VariableReference<string> tableName, VariableReference<string> partitionKey, VariableReference<string> rowKey)
    : ArtifactReference<TableStorageEntityArtifactReference<T>, TableStorageEntityArtifactDescriber<T>, TableStorageEntityArtifactData<T>>
    where T : class, ITableEntity
{
    private string _pinnedTable = "";
    private string _pinnedPartitionKey = "";
    private string _pinnedRowKey = "";

    public StorageAccountIdentifier Identifier { get; } = identifier;

    public override void OnPinReference(VariableStore variableStore, ScopedLogger logger)
    {
        _pinnedTable = tableName.GetRequiredValue(variableStore);
        _pinnedPartitionKey = partitionKey.GetRequiredValue(variableStore);
        _pinnedRowKey = rowKey.GetRequiredValue(variableStore);
        CanDeconstruct = true;
    }

    public override async Task<ArtifactResolveResult<TableStorageEntityArtifactDescriber<T>, TableStorageEntityArtifactData<T>, TableStorageEntityArtifactReference<T>>> ResolveToDataAsync(
        IServiceProvider serviceProvider, ArtifactVersionIdentifier versionIdentifier, VariableStore variableStore, ScopedLogger logger)
    {
        StorageAccountConfig config = serviceProvider.GetRequiredService<ConfigStore<StorageAccountConfig>>().GetConfig(Identifier);
        TableServiceClient serviceClient = new TableServiceClient(config.ConnectionString);
        TableClient tableClient = serviceClient.GetTableClient(GetTableName(variableStore));

        try
        {
            Response<T> response = await tableClient.GetEntityAsync<T>(GetPartitionKey(variableStore), GetRowKey(variableStore));
            return new ArtifactResolveResult<TableStorageEntityArtifactDescriber<T>, TableStorageEntityArtifactData<T>, TableStorageEntityArtifactReference<T>>
            {
                Found = true,
                Data = new TableStorageEntityArtifactData<T>(response.Value) { Identifier = versionIdentifier }
            };
        }
        catch (RequestFailedException e) when (e.Status == 404)
        {
            return new ArtifactResolveResult<TableStorageEntityArtifactDescriber<T>, TableStorageEntityArtifactData<T>, TableStorageEntityArtifactReference<T>>
            {
                Found = false
            };
        }
    }

    public override void DeclareIO(StepIOContract contract)
    {
        if (tableName.HasIdentifier)
            contract.Inputs.Add(new StepIOEntry(tableName.Identifier!.Identifier, StepIOKind.Variable, true, typeof(string)));
        if (partitionKey.HasIdentifier)
            contract.Inputs.Add(new StepIOEntry(partitionKey.Identifier!.Identifier, StepIOKind.Variable, true, typeof(string)));
        if (rowKey.HasIdentifier)
            contract.Inputs.Add(new StepIOEntry(rowKey.Identifier!.Identifier, StepIOKind.Variable, true, typeof(string)));
    }

    public string GetTableName(VariableStore variableStore) => IsPinned ? _pinnedTable : tableName.GetRequiredValue(variableStore);
    public string GetPartitionKey(VariableStore variableStore) => IsPinned ? _pinnedPartitionKey : partitionKey.GetRequiredValue(variableStore);
    public string GetRowKey(VariableStore variableStore) => IsPinned ? _pinnedRowKey : rowKey.GetRequiredValue(variableStore);

    public override string ToString() => IsPinned
        ? $"Azure Table<{typeof(T).Name}>: {_pinnedTable}/{_pinnedPartitionKey}/{_pinnedRowKey}"
        : $"Azure Table<{typeof(T).Name}> (unresolved)";
}
