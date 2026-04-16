using System;

namespace TestFramework.Azure.Configuration.SpecificConfigs;

public record StorageAccountConfig
{
    public required string ConnectionString { get; init; }
    public required string? QueueContainerName { get; init; }
    public required string? BlobContainerName { get; init; }
    public required string? TableContainerName { get; init; }

    public string QueueContainerNameRequired => QueueContainerName ?? throw new InvalidOperationException("QueueContainerName is required.");
    public string BlobContainerNameRequired => BlobContainerName ?? throw new InvalidOperationException("BlobContainerName is required.");
    public string TableContainerNameRequired => TableContainerName ?? throw new InvalidOperationException("TableContainerName is required.");
}