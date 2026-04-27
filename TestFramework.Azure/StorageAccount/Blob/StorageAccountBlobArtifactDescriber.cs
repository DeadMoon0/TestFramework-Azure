using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TestFramework.Azure.Configuration;
using TestFramework.Azure.Configuration.SpecificConfigs;
using TestFramework.Azure.Runtime;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Logging;
using TestFramework.Core.Variables;

namespace TestFramework.Azure.StorageAccount.Blob;

/// <summary>
/// Sets up and tears down blob artifacts.
/// </summary>
public class StorageAccountBlobArtifactDescriber : ArtifactDescriber<StorageAccountBlobArtifactDescriber, StorageAccountBlobArtifactData, StorageAccountBlobArtifactReference>
{
    /// <summary>
    /// Deletes the referenced blob during artifact cleanup.
    /// </summary>
    public override async Task Deconstruct(IServiceProvider serviceProvider, StorageAccountBlobArtifactReference reference, VariableStore variableStore, ScopedLogger logger)
    {
        StorageAccountConfig config = serviceProvider.GetRequiredService<ConfigStore<StorageAccountConfig>>().GetConfig(reference.Identifier);
        IBlobContainerAdapter container = serviceProvider.GetAzureComponentFactory().Blob.CreateContainer(config);
        await container.DeleteBlobAsync(reference.GetPath(variableStore));

        logger.LogInformation($"Blob {reference.GetPath(variableStore)} deleted.");
    }

    /// <summary>
    /// Uploads the referenced blob during artifact setup.
    /// </summary>
    public override async Task Setup(IServiceProvider serviceProvider, StorageAccountBlobArtifactData data, StorageAccountBlobArtifactReference reference, VariableStore variableStore, ScopedLogger logger)
    {
        StorageAccountConfig config = serviceProvider.GetRequiredService<ConfigStore<StorageAccountConfig>>().GetConfig(reference.Identifier);
        IBlobContainerAdapter container = serviceProvider.GetAzureComponentFactory().Blob.CreateContainer(config);
        await container.CreateIfNotExistsAsync();
        await container.UploadBlobAsync(reference.GetPath(variableStore), data.Data, data.MetaData);

        logger.LogInformation($"Blob {reference.GetPath(variableStore)} uploaded.");
    }

    /// <summary>
    /// Returns a readable string representation of the describer.
    /// </summary>
    /// <returns>A string representation of the describer.</returns>
    public override string ToString() => "Azure Storage Blob";
}