namespace TestFramework.Azure.FunctionApp;

/// <summary>
/// Reserved resource-setting keys used to connect Function App steps with Azure resource identifiers.
/// </summary>
public static class FunctionAppResourceSettingNames
{
    /// <summary>
    /// Resource setting key for the backing Storage Account identifier.
    /// </summary>
    public const string StorageIdentifier = "TestFramework:Azure:StorageIdentifier";

    /// <summary>
    /// Resource setting key for the backing Cosmos DB identifier.
    /// </summary>
    public const string CosmosIdentifier = "TestFramework:Azure:CosmosIdentifier";

    /// <summary>
    /// Resource setting key for the Service Bus trigger identifier.
    /// </summary>
    public const string ServiceBusTriggerIdentifier = "TestFramework:Azure:ServiceBusTriggerIdentifier";

    /// <summary>
    /// Resource setting key for the Service Bus reply identifier.
    /// </summary>
    public const string ServiceBusReplyIdentifier = "TestFramework:Azure:ServiceBusReplyIdentifier";
}