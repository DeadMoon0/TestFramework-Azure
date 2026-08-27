using TestFramework.Azure.Configuration.SpecificConfigs;
using TestFramework.Core.Steps;

namespace TestFramework.Azure.Runtime;

/// <summary>
/// The one door between "where is this resource" and "give me a client for it".
/// </summary>
/// <remarks>
/// <para>
/// Each resource type already has a resolver that reads what was declared and overlays what the run
/// produced. What was missing is that nothing obliged a caller to use it: a client factory took the
/// configuration record, and a record straight from <c>ConfigStore&lt;T&gt;</c> is the same type. This class
/// mints the <see cref="Resolved{TConfig}"/> the factories now demand, and it is the only thing that can -
/// so every client in the package is built from coordinates the run stands behind.
/// </para>
/// <para>
/// It is deliberately not a second resolver. Each method delegates to the resolver that owns the type, which
/// stays next to that type; this is the gate, not the logic.
/// </para>
/// </remarks>
internal static class AzureResources
{
    /// <summary>Where a storage account is, for a blob or table client.</summary>
    /// <param name="context">The run.</param>
    /// <param name="identifier">Which account.</param>
    /// <returns>The account's configuration, resolved.</returns>
    internal static Resolved<StorageAccountConfig> StorageAccount(RunContext context, string identifier)
        => Mint(TestFramework.Azure.StorageAccount.StorageAccountResources.Resolve(context, identifier));

    /// <summary>Where a Cosmos container is.</summary>
    /// <param name="context">The run.</param>
    /// <param name="identifier">Which container.</param>
    /// <returns>The container's configuration, resolved.</returns>
    internal static Resolved<CosmosContainerDbConfig> Cosmos(RunContext context, string identifier)
        => Mint(TestFramework.Azure.DB.CosmosDB.CosmosResources.Resolve(context, identifier));

    /// <summary>Where a Service Bus namespace is.</summary>
    /// <param name="context">The run.</param>
    /// <param name="identifier">Which namespace entry.</param>
    /// <returns>The namespace's configuration, resolved.</returns>
    internal static Resolved<ServiceBusConfig> ServiceBus(RunContext context, string identifier)
        => Mint(TestFramework.Azure.ServiceBus.ServiceBusResources.Resolve(context, identifier));

    private static Resolved<TConfig> Mint<TConfig>(TConfig resolved) => new Minted<TConfig>(resolved);

    /// <summary>
    /// The only subclass, and private, so this class is the only source of a resolved record.
    /// </summary>
    /// <remarks>
    /// An enclosing type cannot reach a nested type's private constructor, which is why the token is an
    /// abstract base with a <c>private protected</c> constructor and this is a private subclass of it -
    /// the same shape the artifact write ticket uses in Core.
    /// </remarks>
    private sealed class Minted<TConfig>(TConfig value) : Resolved<TConfig>(value);
}
