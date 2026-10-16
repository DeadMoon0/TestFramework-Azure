namespace TestFramework.Azure.Trigger.IsLive;

/// <summary>
/// Known Azure resource categories returned by liveness checks.
/// </summary>
public enum AzureKnownResourceKind
{
    /// <summary>
    /// A resource kind that is not recognized by the current SDK version.
    /// </summary>
    Unknown,

    /// <summary>
    /// Azure Function App.
    /// </summary>
    FunctionApp,

    /// <summary>
    /// Azure Logic App.
    /// </summary>
    LogicApp,

    /// <summary>
    /// Azure Service Bus.
    /// </summary>
    ServiceBus,

    /// <summary>
    /// Azure Storage.
    /// </summary>
    Storage,

    /// <summary>
    /// Azure Cosmos DB.
    /// </summary>
    Cosmos,

    /// <summary>
    /// Azure SQL.
    /// </summary>
    Sql
}

internal static class AzureKnownResourceKindResolver
{
    internal static AzureKnownResourceKind FromIdentifier(string resourceKind)
    {
        return resourceKind switch
        {
            AzureResourceKinds.FunctionApp => AzureKnownResourceKind.FunctionApp,
            AzureResourceKinds.LogicApp => AzureKnownResourceKind.LogicApp,
            AzureResourceKinds.ServiceBus => AzureKnownResourceKind.ServiceBus,
            AzureResourceKinds.Storage => AzureKnownResourceKind.Storage,
            AzureResourceKinds.Cosmos => AzureKnownResourceKind.Cosmos,
            AzureResourceKinds.Sql => AzureKnownResourceKind.Sql,
            _ => AzureKnownResourceKind.Unknown
        };
    }
}