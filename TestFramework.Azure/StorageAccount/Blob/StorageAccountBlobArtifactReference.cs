using TestFramework.Core.Steps;
using Azure.Storage.Blobs;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Threading.Tasks;
using TestFramework.Azure.Configuration;
using TestFramework.Azure.Configuration.SpecificConfigs;
using TestFramework.Azure.Identifier;
using TestFramework.Azure.Runtime;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Variables;

namespace TestFramework.Azure.StorageAccount.Blob;

/// <summary>
/// Reference to a blob inside the configured blob container.
/// </summary>
/// <param name="identifier">The Storage Account identifier.</param>
/// <param name="path">The blob path variable.</param>
public class StorageAccountBlobArtifactReference(StorageAccountIdentifier identifier, VariableReference<string> path) : ArtifactReference<StorageAccountBlobArtifactReference, StorageAccountBlobArtifactDescriber, StorageAccountBlobArtifactData>
{
    private string pinnedPath = "";

    /// <summary>
    /// The Storage Account identifier used to resolve the blob container.
    /// </summary>
    public StorageAccountIdentifier Identifier { get; } = identifier;

    /// <summary>
    /// Pins the reference to a concrete blob path.
    /// </summary>
    public override void OnPinReference(RunContext context)
    {
        this.pinnedPath = path.GetRequiredValue(context.Variables);
        CanDeconstruct = true;
    }

    /// <summary>
    /// Resolves the reference into concrete blob artifact data.
    /// </summary>
    public override async Task<ArtifactResolveResult<StorageAccountBlobArtifactDescriber, StorageAccountBlobArtifactData, StorageAccountBlobArtifactReference>> ResolveToDataAsync(RunContext context, ArtifactVersionIdentifier versionIdentifier)
    {
        IBlobContainerAdapter container = context.Services.GetAzureComponentFactory()
            .Blob.CreateContainer(AzureResources.StorageAccount(context, Identifier));
        BlobReadResponse blob = await container.ReadBlobAsync(GetPath(context.Variables));
        if (!blob.Found) return new ArtifactResolveResult<StorageAccountBlobArtifactDescriber, StorageAccountBlobArtifactData, StorageAccountBlobArtifactReference> { Found = false };
        return new ArtifactResolveResult<StorageAccountBlobArtifactDescriber, StorageAccountBlobArtifactData, StorageAccountBlobArtifactReference>
        {
            Found = true,
            Data = new StorageAccountBlobArtifactData(blob.Data ?? [], blob.Metadata?.ToDictionary() ?? [])
            {
                Identifier = versionIdentifier
            }
        };
    }

    /// <summary>
    /// Declares the variable inputs required by the reference.
    /// </summary>
    public override void DeclareIO(StepIOContract contract)
    {
        if (path.HasIdentifier)
            contract.Inputs.Add(new StepIOEntry(path.Identifier!.Identifier, StepIOKind.Variable, true, typeof(string)));
    }

    /// <summary>
    /// Gets the blob path, using the pinned value when the reference has been pinned.
    /// </summary>
    /// <returns>The blob path.</returns>
    public string GetPath(VariableStore variableStore)
    {
        if (IsPinned) return this.pinnedPath;
        return path.GetRequiredValue(variableStore);
    }

    /// <summary>
    /// Returns a readable string representation of the reference.
    /// </summary>
    /// <returns>A string representation of the reference.</returns>
    public override string ToString() => IsPinned
        ? $"Azure Blob: {Identifier}/{pinnedPath}"
        : "Azure Blob (unresolved)";
}