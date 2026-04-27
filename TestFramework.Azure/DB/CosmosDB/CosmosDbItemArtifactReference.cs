using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using TestFramework.Azure.Configuration;
using TestFramework.Azure.Configuration.SpecificConfigs;
using TestFramework.Azure.Identifier;
using TestFramework.Azure.Runtime;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Variables;

namespace TestFramework.Azure.DB.CosmosDB;

/// <summary>
/// Reference to a Cosmos DB item addressed by id and partition key.
/// </summary>
/// <typeparam name="TItem">The item type.</typeparam>
public class CosmosDbItemArtifactReference<TItem> : ArtifactReference<CosmosDbItemArtifactReference<TItem>, CosmosDbItemArtifactDescriber<TItem>, CosmosDbItemArtifactData<TItem>>
{
    private string pinnedId = "";
    private PartitionKey pinnedPartitionKey = PartitionKey.Null;
    private VariableReference<PartitionKey>? _partitionKey;
    private VariableReference<string>? _id;

    /// <summary>
    /// Initializes a Cosmos DB artifact reference.
    /// </summary>
    /// <param name="dbIdentifier">The Cosmos container identifier.</param>
    /// <param name="partitionKey">Optional partition key reference.</param>
    /// <param name="id">Optional document id reference.</param>
    public CosmosDbItemArtifactReference(CosmosContainerIdentifier dbIdentifier, VariableReference<PartitionKey>? partitionKey = null, VariableReference<string>? id = null)
    {
        this._partitionKey = partitionKey;
        this._id = id;
        DbIdentifier = dbIdentifier;

        CanDeconstruct = partitionKey is not null && id is not null;
    }

    /// <summary>
    /// The Cosmos container identifier used to resolve the container.
    /// </summary>
    public CosmosContainerIdentifier DbIdentifier { get; }

    /// <summary>
    /// Pins the reference to concrete id and partition key values.
    /// </summary>
    public override void OnPinReference(VariableStore variableStore, ScopedLogger logger)
    {
        pinnedId = this._id?.GetRequiredValue(variableStore) ?? "";
        pinnedPartitionKey = this._partitionKey?.GetRequiredValue(variableStore, "PartitionKey is required.") ?? PartitionKey.Null;
    }

    /// <summary>
    /// Resolves the reference into concrete artifact data.
    /// </summary>
    public override async Task<ArtifactResolveResult<CosmosDbItemArtifactDescriber<TItem>, CosmosDbItemArtifactData<TItem>, CosmosDbItemArtifactReference<TItem>>> ResolveToDataAsync(IServiceProvider serviceProvider, ArtifactVersionIdentifier versionIdentifier, VariableStore variableStore, ScopedLogger logger)
    {
        CosmosContainerDbConfig config = serviceProvider.GetRequiredService<ConfigStore<CosmosContainerDbConfig>>().GetConfig(DbIdentifier);
        ICosmosContainerAdapter container = serviceProvider.GetAzureComponentFactory().Cosmos.CreateContainer(config);

        CosmosReadResponse itemResp = await container.ReadItemAsync(GetId(variableStore), GetPartitionKey(variableStore));
        if (!itemResp.Found)
            return new ArtifactResolveResult<CosmosDbItemArtifactDescriber<TItem>, CosmosDbItemArtifactData<TItem>, CosmosDbItemArtifactReference<TItem>> { Found = false };

        if (itemResp.Content is not null)
        {
            CosmosDbItemArtifactData<TItem> data = new CosmosDbItemArtifactData<TItem>(FromStream<TItem>(itemResp.Content)) { Identifier = versionIdentifier };
            return new ArtifactResolveResult<CosmosDbItemArtifactDescriber<TItem>, CosmosDbItemArtifactData<TItem>,
                CosmosDbItemArtifactReference<TItem>>
            {
                Data = data,
                Found = true,
            };
        }

        throw new UnreachableException();
    }

    /// <summary>
    /// Declares the variable inputs required by the reference.
    /// </summary>
    public override void DeclareIO(StepIOContract contract)
    {
        if (_partitionKey is not null && _partitionKey.HasIdentifier)
            contract.Inputs.Add(new StepIOEntry(_partitionKey.Identifier!.Identifier, StepIOKind.Variable, false));
        if (_id is not null && _id.HasIdentifier)
            contract.Inputs.Add(new StepIOEntry(_id.Identifier!.Identifier, StepIOKind.Variable, false));
    }

    internal PartitionKey GetPartitionKey(VariableStore variableStore)
    {
        if (this._partitionKey is null) throw new InvalidOperationException("Cannot get the PartitionKey when it is not Set.");
        if (IsPinned) return pinnedPartitionKey;
        return this._partitionKey.GetRequiredValue(variableStore);
    }

    internal string GetId(VariableStore variableStore)
    {
        if (this._id is null) throw new InvalidOperationException("Cannot get the Id when it is not Set.");
        if (IsPinned) return pinnedId;
        return this._id.GetRequiredValue(variableStore);
    }

    internal void SetIdentifier(string _id, PartitionKey _partitionKey)
    {
        this._id = Var.Const(_id);
        pinnedId = _id;
        this._partitionKey = Var.Const(_partitionKey);
        pinnedPartitionKey = _partitionKey;
        CanDeconstruct = true;
    }

    private static T FromStream<T>(Stream stream)
    {
        using (stream)
        {
            if (typeof(Stream).IsAssignableFrom(typeof(T)))
            {
                return (T)(object)stream;
            }

            using (StreamReader sr = new StreamReader(stream))
            {
                using (JsonTextReader jsonTextReader = new JsonTextReader(sr))
                {
                    return JsonSerializer.CreateDefault().Deserialize<T>(jsonTextReader) ?? throw new InvalidOperationException("Could not read and parse JSON from CosmosDb.");
                }
            }
        }
    }

    /// <summary>
    /// Returns a readable string representation of the reference.
    /// </summary>
    /// <returns>A string representation of the reference.</returns>
    public override string ToString() => IsPinned
        ? $"Cosmos DB<{typeof(TItem).Name}>: {DbIdentifier}/{pinnedId}"
        : $"Cosmos DB<{typeof(TItem).Name}> (unresolved)";
}