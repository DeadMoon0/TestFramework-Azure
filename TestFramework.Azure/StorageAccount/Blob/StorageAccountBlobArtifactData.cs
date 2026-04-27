using System.Collections.Generic;
using TestFramework.Core.Artifacts;

namespace TestFramework.Azure.StorageAccount.Blob;

/// <summary>
/// Artifact payload that stores a blob snapshot.
/// </summary>
/// <param name="data">The blob bytes.</param>
/// <param name="metaData">The blob metadata.</param>
public class StorageAccountBlobArtifactData(byte[] data, Dictionary<string, string> metaData) : ArtifactData<StorageAccountBlobArtifactData, StorageAccountBlobArtifactDescriber, StorageAccountBlobArtifactReference>
{
    /// <summary>
    /// The blob bytes.
    /// </summary>
    public byte[] Data { get; } = [.. data];

    /// <summary>
    /// The blob metadata snapshot.
    /// </summary>
    public IReadOnlyDictionary<string, string> MetaData { get; } = metaData.AsReadOnly();

    /// <summary>
    /// Returns a readable string representation of the blob artifact.
    /// </summary>
    /// <returns>A string representation of the artifact data.</returns>
    public override string ToString() => $"Blob [{Data.Length} bytes, {MetaData.Count} metadata entries]";
}