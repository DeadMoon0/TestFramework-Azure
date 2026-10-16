using System;

namespace TestFramework.Azure.Configuration.SpecificConfigs;

/// <summary>
/// Configuration required to reach Azure Storage resources used by blobs, tables, and optional queues.
/// </summary>
/// <remarks>
/// The identifier maps to a named entry under the <c>StorageAccount</c> section.
/// <see cref="ConnectionString"/> is always required. Container/table names are required only for the Azure features that use them.
/// </remarks>
public sealed record StorageAccountConfig
{
    // Its text redacts its credentials; see ConfigRecordText.
    private bool PrintMembers(System.Text.StringBuilder builder) => ConfigRecordText.PrintMembers(this, builder);

    /// <summary>
    /// Storage account connection string.
    /// </summary>
    [Secret]
    public required string ConnectionString { get; init; }

    /// <summary>
    /// Optional queue name when queue-backed storage operations need a default queue.
    /// </summary>
    public string? QueueName { get; init; }

    /// <summary>
    /// Optional blob container name used by blob artifact helpers and blob liveness checks.
    /// </summary>
    public string? BlobContainerName { get; init; }

    /// <summary>
    /// Optional table name used by table artifact helpers and table liveness checks.
    /// </summary>
    public string? TableName { get; init; }

    /// <summary>
    /// Gets the configured queue name or throws when no queue name has been supplied.
    /// </summary>
    public string QueueNameRequired => QueueName ?? throw new TestFramework.Core.Exceptions.FrameworkConfigurationException("QueueName is required.");

    /// <summary>
    /// Gets the configured blob container name or throws when no blob container name has been supplied.
    /// </summary>
    public string BlobContainerNameRequired => BlobContainerName ?? throw new TestFramework.Core.Exceptions.FrameworkConfigurationException("BlobContainerName is required.");

    /// <summary>
    /// Gets the configured table name or throws when no table name has been supplied.
    /// </summary>
    public string TableNameRequired => TableName ?? throw new TestFramework.Core.Exceptions.FrameworkConfigurationException("TableName is required.");
}