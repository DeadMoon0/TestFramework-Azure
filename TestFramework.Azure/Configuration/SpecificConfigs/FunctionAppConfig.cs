namespace TestFramework.Azure.Configuration.SpecificConfigs;

public record FunctionAppConfig
{
    public required string BaseUrl { get; init; }
    public required string Code { get; init; }
    public string? AdminCode { get; init; }
}