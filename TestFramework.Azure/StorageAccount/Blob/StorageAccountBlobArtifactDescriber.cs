using TestFramework.Core.Steps;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TestFramework.Azure.Configuration;
using TestFramework.Azure.Configuration.SpecificConfigs;
using TestFramework.Azure.Runtime;
using Newtonsoft.Json.Linq;
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
    /// The canonical key mirrored from <c>TestFramework.Core.Debugger.DebugValueSchemaKeys.Blob</c>.
    /// </summary>
    /// <remarks>
    /// A literal rather than a reference because this package builds against the published Core;
    /// <c>ArtifactSchemaKeyTests</c> pins it to the canonical value so the two cannot drift.
    /// </remarks>
    internal const string SchemaKey = "tf.artifact.blob";

    /// <summary>
    /// Identifies this artifact's debug shape, so a consumer renders it as a storage blob instead of
    /// falling back to a generic view keyed on the CLR type name.
    /// </summary>
    public override string DebugValueSchemaKey => SchemaKey;

    /// <inheritdoc />
    public override JToken? CreateDebugValueCustomPayload(ArtifactInstanceGeneric instance)
    {
        ArgumentNullException.ThrowIfNull(instance);

        if (instance.Reference is not StorageAccountBlobArtifactReference)
            return null;

        return new JObject
        {
            ["key"] = instance.Identifier.Identifier,
            ["blob"] = instance.Identifier.Identifier
        };
    }

    /// <summary>
    /// Deletes the referenced blob during artifact cleanup.
    /// </summary>
    public override async Task Deconstruct(RunContext context, StorageAccountBlobArtifactReference reference)
    {
        StorageAccountConfig config = StorageAccountResources.Resolve(context, reference.Identifier);
        IBlobContainerAdapter container = context.Services.GetAzureComponentFactory().Blob.CreateContainer(config);
        await container.DeleteBlobAsync(reference.GetPath(context.Variables));

        context.Logger.LogInformation($"Blob {reference.GetPath(context.Variables)} deleted.");
    }

    /// <summary>
    /// Uploads the referenced blob during artifact setup.
    /// </summary>
    public override async Task Setup(RunContext context, StorageAccountBlobArtifactData data, StorageAccountBlobArtifactReference reference)
    {
        StorageAccountConfig config = StorageAccountResources.Resolve(context, reference.Identifier);
        IBlobContainerAdapter container = context.Services.GetAzureComponentFactory().Blob.CreateContainer(config);
        await container.CreateIfNotExistsAsync();
        await container.UploadBlobAsync(reference.GetPath(context.Variables), data.Data, data.MetaData);

        context.Logger.LogInformation($"Blob {reference.GetPath(context.Variables)} uploaded.");
    }

    /// <summary>
    /// Returns a readable string representation of the describer.
    /// </summary>
    /// <returns>A string representation of the describer.</returns>
    public override string ToString() => "Azure Storage Blob";
}