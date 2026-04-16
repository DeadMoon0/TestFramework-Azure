using Azure.Storage.Blobs;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Threading.Tasks;
using TestFramework.Azure.Configuration;
using TestFramework.Azure.Configuration.SpecificConfigs;
using TestFramework.Azure.Identifier;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Variables;

namespace TestFramework.Azure.StorageAccount.Blob;

public class StorageAccountBlobArtifactReference(StorageAccountIdentifier identifier, VariableReference<string> path) : ArtifactReference<StorageAccountBlobArtifactReference, StorageAccountBlobArtifactDescriber, StorageAccountBlobArtifactData>
{
    private string pinnedPath = "";

    public StorageAccountIdentifier Identifier { get; } = identifier;

    public override void OnPinReference(VariableStore variableStore, ScopedLogger logger)
    {
        this.pinnedPath = path.GetRequiredValue(variableStore);
        CanDeconstruct = true;
    }

    public override async Task<ArtifactResolveResult<StorageAccountBlobArtifactDescriber, StorageAccountBlobArtifactData, StorageAccountBlobArtifactReference>> ResolveToDataAsync(IServiceProvider serviceProvider, ArtifactVersionIdentifier versionIdentifier, VariableStore variableStore, ScopedLogger logger)
    {
        StorageAccountConfig config = serviceProvider.GetRequiredService<ConfigStore<StorageAccountConfig>>().GetConfig(Identifier);
        BlobServiceClient client = new BlobServiceClient(config.ConnectionString);
        BlobContainerClient container = client.GetBlobContainerClient(config.BlobContainerNameRequired);
        BlobClient blob = container.GetBlobClient(GetPath(variableStore));
        if (!await blob.ExistsAsync()) return new ArtifactResolveResult<StorageAccountBlobArtifactDescriber, StorageAccountBlobArtifactData, StorageAccountBlobArtifactReference> { Found = false };
        return new ArtifactResolveResult<StorageAccountBlobArtifactDescriber, StorageAccountBlobArtifactData, StorageAccountBlobArtifactReference>
        {
            Found = true,
            Data = new StorageAccountBlobArtifactData((await blob.DownloadContentAsync()).Value.Content.ToArray(), (await blob.GetPropertiesAsync()).Value.Metadata.ToDictionary())
            {
                Identifier = versionIdentifier
            }
        };
    }

    public override void DeclareIO(StepIOContract contract)
    {
        if (path.HasIdentifier)
            contract.Inputs.Add(new StepIOEntry(path.Identifier!.Identifier, StepIOKind.Variable, true, typeof(string)));
    }

    public string GetPath(VariableStore variableStore)
    {
        if (IsPinned) return this.pinnedPath;
        return path.GetRequiredValue(variableStore);
    }

    public override string ToString() => IsPinned
        ? $"Azure Blob: {Identifier}/{pinnedPath}"
        : "Azure Blob (unresolved)";
}