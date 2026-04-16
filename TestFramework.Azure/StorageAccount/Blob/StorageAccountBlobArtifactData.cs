using System.Collections.Generic;
using TestFramework.Core.Artifacts;

namespace TestFramework.Azure.StorageAccount.Blob;

public class StorageAccountBlobArtifactData(byte[] data, Dictionary<string, string> metaData) : ArtifactData<StorageAccountBlobArtifactData, StorageAccountBlobArtifactDescriber, StorageAccountBlobArtifactReference>
{
    public byte[] Data { get; } = [.. data];
    public IReadOnlyDictionary<string, string> MetaData { get; } = metaData.AsReadOnly();

    public override string ToString() => $"Blob [{Data.Length} bytes, {MetaData.Count} metadata entries]";
}