using TestFramework.Core.Steps;
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

namespace TestFramework.Azure.DB.CosmosDb;

/// <summary>
/// Reference to a Cosmos DB item addressed by id and partition key.
/// </summary>
/// <remarks>
/// Made with <c>AzureExt.Artifact.DB.CosmosDbRef(...)</c>, which names the item, or by <c>AddCosmosDbItemArtifact</c>,
/// which leaves it to setup to read the id and partition key from the item it writes. Either way the reference
/// addresses exactly one item, so teardown deletes that one.
/// </remarks>
/// <typeparam name="TItem">The item type.</typeparam>
public sealed class CosmosDbItemArtifactReference<TItem> : ArtifactReference<CosmosDbItemArtifactReference<TItem>, CosmosDbItemArtifactDescriber<TItem>, CosmosDbItemArtifactData<TItem>>
{
    private string pinnedId = "";
    private PartitionKey pinnedPartitionKey = PartitionKey.Null;
    private VariableReference<PartitionKey>? _partitionKey;
    private VariableReference<string>? _id;

    /// <summary>
    /// A reference that names its item.
    /// </summary>
    /// <param name="dbIdentifier">The Cosmos container identifier.</param>
    /// <param name="partitionKey">The partition key.</param>
    /// <param name="id">The document id.</param>
    internal CosmosDbItemArtifactReference(CosmosDbIdentifier dbIdentifier, VariableReference<PartitionKey> partitionKey, VariableReference<string> id)
    {
        ArgumentNullException.ThrowIfNull(dbIdentifier);
        ArgumentNullException.ThrowIfNull(partitionKey);
        ArgumentNullException.ThrowIfNull(id);

        this._partitionKey = partitionKey;
        this._id = id;
        DbIdentifier = dbIdentifier;
        namedByTheTest = true;
        CanDeconstruct = true;
    }

    /// <summary>
    /// A reference whose item setup names: the id and partition key are read from the item it writes.
    /// </summary>
    /// <param name="dbIdentifier">The Cosmos container identifier.</param>
    internal CosmosDbItemArtifactReference(CosmosDbIdentifier dbIdentifier)
    {
        ArgumentNullException.ThrowIfNull(dbIdentifier);

        DbIdentifier = dbIdentifier;

        // Nothing to delete until setup has written an item and assigned its identity.
        CanDeconstruct = false;
    }

    private readonly bool namedByTheTest;

    /// <summary>
    /// Whether the reference names its item, or waits for setup to read the identity from the item it writes.
    /// </summary>
    internal bool NamesItsItem => _id is not null && _partitionKey is not null;

    /// <summary>
    /// The Cosmos container identifier used to resolve the container.
    /// </summary>
    public CosmosDbIdentifier DbIdentifier { get; }

    /// <summary>
    /// Pins the reference to concrete id and partition key values.
    /// </summary>
    public override void OnPinReference(RunContext context)
    {
        pinnedId = this._id?.GetRequiredValue(context.Variables) ?? "";
        pinnedPartitionKey = this._partitionKey?.GetRequiredValue(context.Variables, "PartitionKey is required.") ?? PartitionKey.Null;
    }

    /// <summary>
    /// Resolves the reference into concrete artifact data.
    /// </summary>
    public override async Task<ArtifactResolveResult<CosmosDbItemArtifactDescriber<TItem>, CosmosDbItemArtifactData<TItem>, CosmosDbItemArtifactReference<TItem>>> ResolveToDataAsync(RunContext context, ArtifactVersionIdentifier versionIdentifier)
    {
        ICosmosContainerAdapter container = context.Services.GetAzureComponentFactory()
            .Cosmos.CreateContainer(AzureResources.For<CosmosDbConfig>(context, DbIdentifier));

        CosmosReadResponse itemResp = await container.ReadItemAsync(GetId(context.Variables), GetPartitionKey(context.Variables), context.Deadline.Token);
        if (!itemResp.Found)
            return new ArtifactResolveResult<CosmosDbItemArtifactDescriber<TItem>, CosmosDbItemArtifactData<TItem>, CosmosDbItemArtifactReference<TItem>> { Found = false };

        if (itemResp.Content is not null)
        {
            CosmosDbItemArtifactData<TItem> data = new CosmosDbItemArtifactData<TItem>(CosmosJson.ReadItem<TItem>(itemResp.Content)) { Identifier = versionIdentifier };
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
        contract.OptionalInput(_partitionKey);
        contract.OptionalInput(_id);
    }

    internal PartitionKey GetPartitionKey(VariableStore variableStore)
    {
        if (this._partitionKey is null) throw new TestFramework.Core.Exceptions.FrameworkStateException("Cannot get the PartitionKey when it is not set.");
        if (IsPinned) return pinnedPartitionKey;
        return this._partitionKey.GetRequiredValue(variableStore);
    }

    internal string GetId(VariableStore variableStore)
    {
        if (this._id is null) throw new TestFramework.Core.Exceptions.FrameworkStateException("Cannot get the Id when it is not set.");
        if (IsPinned) return pinnedId;
        return this._id.GetRequiredValue(variableStore);
    }

    /// <summary>
    /// Records the identity of the item setup wrote, for a reference that did not name one.
    /// </summary>
    /// <param name="proof">The store's proof that the run admitted it.</param>
    /// <param name="id">The item's id.</param>
    /// <param name="partitionKey">The item's partition key.</param>
    internal void AssignIdentity(ArtifactAssignment proof, string id, PartitionKey partitionKey)
    {
        ArgumentNullException.ThrowIfNull(proof);
        proof.EnsureOpen();

        // A reference that named its item keeps that name; setup checks the item agrees instead.
        if (namedByTheTest)
            throw new InvalidOperationException($"'{this}' names its item, so it takes no assigned identity.");

        this._id = Var.Const(id);
        pinnedId = id;
        this._partitionKey = Var.Const(partitionKey);
        pinnedPartitionKey = partitionKey;
        CanDeconstruct = true;
    }


    /// <summary>
    /// Returns a readable string representation of the reference.
    /// </summary>
    /// <returns>A string representation of the reference.</returns>
    public override string ToString() => IsPinned
        ? $"Cosmos DB<{typeof(TItem).Name}>: {DbIdentifier}/{pinnedId}"
        : $"Cosmos DB<{typeof(TItem).Name}> (unresolved)";
}