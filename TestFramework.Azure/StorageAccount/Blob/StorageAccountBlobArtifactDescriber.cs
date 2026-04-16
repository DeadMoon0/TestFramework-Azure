using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TestFramework.Azure.Configuration;
using TestFramework.Azure.Configuration.SpecificConfigs;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Logging;
using TestFramework.Core.Variables;

namespace TestFramework.Azure.StorageAccount.Blob;

public class StorageAccountBlobArtifactDescriber : ArtifactDescriber<StorageAccountBlobArtifactDescriber, StorageAccountBlobArtifactData, StorageAccountBlobArtifactReference>
{
    public override async Task Deconstruct(IServiceProvider serviceProvider, StorageAccountBlobArtifactReference reference, VariableStore variableStore, ScopedLogger logger)
    {
        StorageAccountConfig config = serviceProvider.GetRequiredService<ConfigStore<StorageAccountConfig>>().GetConfig(reference.Identifier);
        BlobServiceClient client = new BlobServiceClient(config.ConnectionString);
        BlobContainerClient container = client.GetBlobContainerClient(config.BlobContainerNameRequired);
        await container.DeleteBlobAsync(reference.GetPath(variableStore), DeleteSnapshotsOption.IncludeSnapshots);

        logger.LogInformation($"Blob {reference.GetPath(variableStore)} deleted.");
    }

    public override async Task Setup(IServiceProvider serviceProvider, StorageAccountBlobArtifactData data, StorageAccountBlobArtifactReference reference, VariableStore variableStore, ScopedLogger logger)
    {
        StorageAccountConfig config = serviceProvider.GetRequiredService<ConfigStore<StorageAccountConfig>>().GetConfig(reference.Identifier);
        BlobServiceClient client = new BlobServiceClient(config.ConnectionString);
        BlobContainerClient container = client.GetBlobContainerClient(config.BlobContainerNameRequired);
        await container.CreateIfNotExistsAsync();
        BlobClient blob = container.GetBlobClient(reference.GetPath(variableStore));
        await blob.UploadAsync(new BinaryData(data.Data), true);
        await blob.SetMetadataAsync((IDictionary<string, string>)data.MetaData);

        logger.LogInformation($"Blob {reference.GetPath(variableStore)} uploaded.");
    }

    public override string ToString() => "Azure Storage Blob";
}