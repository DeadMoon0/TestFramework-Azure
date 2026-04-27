namespace TestFramework.Azure;

/// <summary>
/// Canonical Azure resource kind names used for environment requirements.
/// </summary>
public static class AzureEnvironmentResourceKinds
{
    /// <summary>
    /// Function App environment requirement kind.
    /// </summary>
    public const string FunctionApp = "azure.functionapp";

    /// <summary>
    /// Service Bus environment requirement kind.
    /// </summary>
    public const string ServiceBus = "azure.servicebus";

    /// <summary>
    /// Storage Account environment requirement kind.
    /// </summary>
    public const string Storage = "azure.storage";

    /// <summary>
    /// Cosmos DB environment requirement kind.
    /// </summary>
    public const string Cosmos = "azure.cosmos";

    /// <summary>
    /// SQL database environment requirement kind.
    /// </summary>
    public const string Sql = "azure.sql";
}