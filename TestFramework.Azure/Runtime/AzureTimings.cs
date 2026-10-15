using System;

namespace TestFramework.Azure.Runtime;

/// <summary>
/// The fixed waits and limits the Azure package uses where a step's timeout is not the right budget.
/// </summary>
/// <remarks>
/// Every value here is also listed under "Defaults" in the package README. Wherever a step waits for something,
/// the step's own timeout is the budget; these are the pauses between looks and the bounds on single requests
/// inside that budget.
/// </remarks>
internal static class AzureTimings
{
    /// <summary>
    /// How long a Logic App wait pauses between two reads of the run history: one second.
    /// </summary>
    public static readonly TimeSpan LogicAppPollInterval = TimeSpan.FromSeconds(1);

    /// <summary>
    /// How long schema setup pauses between two looks at a Cosmos database or container that is not there yet: one
    /// second.
    /// </summary>
    public static readonly TimeSpan CosmosSchemaPollInterval = TimeSpan.FromSeconds(1);

    /// <summary>
    /// How long schema setup waits in all for a Cosmos database or container it created to become readable: thirty
    /// seconds, after which it fails naming the resource.
    /// </summary>
    public static readonly TimeSpan CosmosSchemaReadyBudget = TimeSpan.FromSeconds(30);

    /// <summary>
    /// How long one Cosmos schema request may take: twenty seconds.
    /// </summary>
    public static readonly TimeSpan CosmosSchemaRequestTimeout = TimeSpan.FromSeconds(20);

    /// <summary>
    /// How long opening a connection to any Azure HTTP endpoint may take: fifteen seconds, so a host that never
    /// answers fails fast instead of hanging on the handshake.
    /// </summary>
    public static readonly TimeSpan HttpConnectTimeout = TimeSpan.FromSeconds(15);

    /// <summary>
    /// How long a pooled HTTP connection is reused: two minutes, so a long-lived test host notices a DNS change
    /// such as a failover or a swapped slot.
    /// </summary>
    public static readonly TimeSpan HttpPooledConnectionLifetime = TimeSpan.FromMinutes(2);
}
