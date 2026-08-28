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
    /// The value name a Function App resource publishes its function key under.
    /// </summary>
    public const string CodeValue = "Code";

    /// <summary>
    /// The value name a Function App resource publishes its admin key under.
    /// </summary>
    public const string AdminCodeValue = "AdminCode";

    /// <summary>
    /// The value name a Service Bus resource publishes its session requirement under.
    /// </summary>
    public const string RequiredSessionValue = "RequiredSession";

    /// <summary>
    /// The value name a Cosmos resource publishes its provisioned throughput under.
    /// </summary>
    public const string ProvisionedThroughputValue = "ProvisionedThroughput";

    /// <summary>
    /// The value name a Cosmos resource publishes its schema-creation tolerance under.
    /// </summary>
    public const string AllowSchemaCreationValue = "AllowSchemaCreation";

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
    /// though it embeds a secret, because it cannot be divided - the address <em>is</em> the string. It is
    /// still declared <c>secret</c>: the flag keeps the value offered to every reader while the bulk
    /// listings a run can snapshot and log show it redacted, which is exactly the shape an
    /// address-that-carries-a-password needs. The containerised SQL emulator's <c>sa</c> password rides
    /// these strings, and a caller may pin a real one.
    /// </para>
    /// </remarks>
    public static readonly ResourceKind FunctionAppKind = ResourceKind
        .Named(FunctionApp)
        .OffersPerVantage(ValueNames.BaseUrl)
        .Offers(RoutePrefixValue, optional: true)
        .Offers(CodeValue, optional: true, secret: true)
        .Offers(AdminCodeValue, optional: true, secret: true)
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
        // Secret although it is an address, because for a Consumption workflow the address IS the
        // credential: the invoke URL's sig is a bearer token, and the package's result records redact
        // it by construction - a listing must not be the second channel that hands it out raw. The
        // Standard host URL gets redacted along with it, which costs a reader an address that never
        // authenticated anyone; the alternative cost was a replayable invoke URL in every snapshot.
        .OffersPerVantage(ValueNames.BaseUrl, optional: true, secret: true)
        .Offers("HostingMode", optional: true)
        .Offers("WorkflowName", optional: true)
        .Offers("Standard:Code", optional: true, secret: true)
        .Offers("Standard:AdminCode", optional: true, secret: true)
        .Offers("Consumption:WorkflowResourceId", optional: true)
        .Build();

    /// <summary>
    /// What a Service Bus namespace offers: how to connect, and which entities were declared on it.
    /// </summary>
    public static readonly ResourceKind ServiceBusKind = ResourceKind
        .Named(ServiceBus)
        .OffersPerVantage(ValueNames.ConnectionString, optional: false, secret: true)
        .Offers(QueueNameValue, optional: true)
        .Offers(TopicNameValue, optional: true)
        .Offers(SubscriptionNameValue, optional: true)
        .Offers(RequiredSessionValue, optional: true)
        .Build();

    /// <summary>
    /// What a storage account offers: how to connect, and the containers declared on it.
    /// </summary>
    public static readonly ResourceKind StorageKind = ResourceKind
        .Named(Storage)
        .OffersPerVantage(ValueNames.ConnectionString, optional: false, secret: true)
        .Offers(QueueNameValue, optional: true)
        .Offers(BlobContainerNameValue, optional: true)
        .Offers(TableContainerNameValue, optional: true)
        .Build();

    /// <summary>
    /// What a Cosmos container offers: how to connect, and which database and container it is.
    /// </summary>
    public static readonly ResourceKind CosmosKind = ResourceKind
        .Named(Cosmos)
        .OffersPerVantage(ValueNames.ConnectionString, optional: false, secret: true)
        .Offers(DatabaseNameValue)
        .Offers(ContainerNameValue)
        .Offers(ProvisionedThroughputValue, optional: true)
        .Offers(AllowSchemaCreationValue, optional: true)
        .Build();

    /// <summary>
    /// What a SQL database offers: how to connect, and what it is called.
    /// </summary>
    public static readonly ResourceKind SqlKind = ResourceKind
        .Named(Sql)
        .OffersPerVantage(ValueNames.ConnectionString, optional: false, secret: true)
        .Offers(DatabaseNameValue)
        .Build();
}
