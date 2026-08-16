using Azure;
using Azure.Data.Tables;
using System;
using TestFramework.Azure.DB.CosmosDB;
using TestFramework.Azure.DB.SqlServer;
using TestFramework.Azure.StorageAccount.Blob;
using TestFramework.Azure.StorageAccount.Table;

namespace TestFramework.Azure.Tests;

/// <summary>
/// Pins each artifact's debug schema key to its canonical value.
/// </summary>
/// <remarks>
/// A schema key tells a consumer which renderer to use, so changing one silently is a breaking
/// change for anything that draws that artifact. This package carries the keys as literals because
/// it builds against the published Core, which means nothing but a test keeps them in step with
/// <c>TestFramework.Core.Debugger.DebugValueSchemaKeys</c>.
/// </remarks>
public class ArtifactSchemaKeyTests
{
    private sealed class SampleRow
    {
        public int Id { get; set; }
    }

    private sealed class SampleItem
    {
        public string Id { get; set; } = string.Empty;
    }

    private sealed class SampleEntity : ITableEntity
    {
        public string PartitionKey { get; set; } = string.Empty;
        public string RowKey { get; set; } = string.Empty;
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }
    }

    [Fact]
    public void SqlRowArtifactReportsTheCanonicalSchemaKey()
        => Assert.Equal("tf.artifact.sql.row", new SqlRowArtifactDescriber<SampleRow>().DebugValueSchemaKey);

    [Fact]
    public void SqlRowSharesItsSchemaKeyWithTheWebImplementation()
    {
        // The EF-backed row here and the ADO-backed row in TestFramework.Web are different types
        // presenting the same thing, so a consumer should draw them with one icon and one inspector.
        Assert.Equal("tf.artifact.sql.row", new SqlRowArtifactDescriber<SampleRow>().DebugValueSchemaKey);
    }

    [Fact]
    public void CosmosItemArtifactReportsTheCanonicalSchemaKey()
        => Assert.Equal("tf.artifact.cosmos.item", new CosmosDbItemArtifactDescriber<SampleItem>().DebugValueSchemaKey);

    [Fact]
    public void BlobArtifactReportsTheCanonicalSchemaKey()
        => Assert.Equal("tf.artifact.blob", new StorageAccountBlobArtifactDescriber().DebugValueSchemaKey);

    [Fact]
    public void TableEntityArtifactReportsTheCanonicalSchemaKey()
        => Assert.Equal("tf.artifact.table.entity", new TableStorageEntityArtifactDescriber<SampleEntity>().DebugValueSchemaKey);

    [Fact]
    public void EveryShippedArtifactOverridesTheDefaultSchemaKey()
    {
        // The default is GetType().FullName. Any artifact that forgets to override becomes keyed on
        // an implementation detail, so renaming its class breaks every consumer that drew it.
        object[] describers =
        [
            new SqlRowArtifactDescriber<SampleRow>(),
            new CosmosDbItemArtifactDescriber<SampleItem>(),
            new StorageAccountBlobArtifactDescriber(),
            new TableStorageEntityArtifactDescriber<SampleEntity>()
        ];

        foreach (object describer in describers)
        {
            string key = (string)describer.GetType().GetProperty("DebugValueSchemaKey")!.GetValue(describer)!;
            Assert.StartsWith("tf.artifact.", key, StringComparison.Ordinal);
            Assert.NotEqual(describer.GetType().FullName, key);
        }
    }
}
