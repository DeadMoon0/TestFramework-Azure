namespace TestFramework.Azure.Configuration.SpecificConfigs;

public record CosmosContainerDbConfig
{
    public required string ConnectionString { get; set; }
    public required string DatabaseName { get; set; }
    public required string ContainerName { get; set; }
    public string? PartitionKeyPath { get; set; }
}