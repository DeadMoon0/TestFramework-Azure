using TestFramework.Core.Environment.Graph;
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
    /// Logic App environment requirement kind.
    /// </summary>
    public const string LogicApp = "azure.logicapp";

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

    /// <summary>
    /// The value name a resource publishes a container, queue, topic, subscription or database name under.
    /// </summary>
    /// <remarks>
    /// A name is one value for every viewpoint: what a queue is called does not depend on who is asking.
    /// Coordinates do, which is why those are the ones declared per viewpoint below.
    /// </remarks>
    public const string QueueNameValue = "QueueName";

    /// <inheritdoc cref="QueueNameValue"/>
    public const string TopicNameValue = "TopicName";

    /// <inheritdoc cref="QueueNameValue"/>
    public const string SubscriptionNameValue = "SubscriptionName";

    /// <inheritdoc cref="QueueNameValue"/>
    public const string DatabaseNameValue = "DatabaseName";

    /// <inheritdoc cref="QueueNameValue"/>
    public const string ContainerNameValue = "ContainerName";

    /// <inheritdoc cref="QueueNameValue"/>
    public const string BlobContainerNameValue = "BlobContainerName";

    /// <inheritdoc cref="QueueNameValue"/>
    public const string TableContainerNameValue = "TableContainerName";

    /// <summary>
    /// The value name a Function App publishes its route prefix under.
    /// </summary>
    public const string RoutePrefixValue = "RoutePrefix";

    /// <summary>
    /// What a Function App offers: where to reach it, and the prefix its routes sit behind.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Named after <see cref="FunctionApp"/> rather than repeating the string, because this and that are the
    /// same resource. There were two kind vocabularies in this family - these constants for environment
    /// requirements, and the graph's own kinds used by nothing outside the engine - and this is where they
    /// meet.
    /// </para>
    /// <para>
    /// Function keys are deliberately absent, and that is the line for every kind here: a coordinate says
    /// where something is and travels with the resource; a credential says who you are and stays in the typed
    /// configuration the steps already read. A connection string is on the coordinate side of that line even
    /// though it embeds a secret, because it cannot be divided - the address <em>is</em> the string.
    /// </para>
    /// </remarks>
    public static readonly ResourceKind FunctionAppKind = ResourceKind
        .Named(FunctionApp)
        .OffersPerVantage(ValueNames.BaseUrl)
        .Offers(RoutePrefixValue, optional: true)
        .Build();

    /// <summary>
    /// What a Logic App offers: where to reach whichever hosting mode it declared.
    /// </summary>
    /// <remarks>
    /// One address, whether it came from the Standard host URL or the Consumption invoke URL. Which of the two
    /// an entry uses is the entry's business; a reader wants the address.
    /// </remarks>
    public static readonly ResourceKind LogicAppKind = ResourceKind
        .Named(LogicApp)
        .OffersPerVantage(ValueNames.BaseUrl, optional: true)
        .Build();

    /// <summary>
    /// What a Service Bus namespace offers: how to connect, and which entities were declared on it.
    /// </summary>
    public static readonly ResourceKind ServiceBusKind = ResourceKind
        .Named(ServiceBus)
        .OffersPerVantage(ValueNames.ConnectionString)
        .Offers(QueueNameValue, optional: true)
        .Offers(TopicNameValue, optional: true)
        .Offers(SubscriptionNameValue, optional: true)
        .Build();

    /// <summary>
    /// What a storage account offers: how to connect, and the containers declared on it.
    /// </summary>
    public static readonly ResourceKind StorageKind = ResourceKind
        .Named(Storage)
        .OffersPerVantage(ValueNames.ConnectionString)
        .Offers(QueueNameValue, optional: true)
        .Offers(BlobContainerNameValue, optional: true)
        .Offers(TableContainerNameValue, optional: true)
        .Build();

    /// <summary>
    /// What a Cosmos container offers: how to connect, and which database and container it is.
    /// </summary>
    public static readonly ResourceKind CosmosKind = ResourceKind
        .Named(Cosmos)
        .OffersPerVantage(ValueNames.ConnectionString)
        .Offers(DatabaseNameValue)
        .Offers(ContainerNameValue)
        .Build();

    /// <summary>
    /// What a SQL database offers: how to connect, and what it is called.
    /// </summary>
    public static readonly ResourceKind SqlKind = ResourceKind
        .Named(Sql)
        .OffersPerVantage(ValueNames.ConnectionString)
        .Offers(DatabaseNameValue)
        .Build();
}
