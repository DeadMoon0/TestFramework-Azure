using System;
using Azure.Messaging.ServiceBus;
using TestFramework.Azure.Runtime;
using TestFramework.Azure.ServiceBus;
using Xunit;

namespace TestFramework.Azure.Tests;

/// <summary>
/// The rules a Service Bus wait applies to each delivery and each processor error.
/// </summary>
/// <remarks>
/// These decide whether a run passes, and they used to live inside the processor's handlers where nothing could test
/// them without a broker. The one that changed: a transient error the processor retries by itself no longer fails the
/// wait.
/// </remarks>
public sealed class ServiceBusSettlementTests
{
    private static ServiceBusReceiveRequest Request(bool complete, bool abandonNonMatching)
        => new(null, null, null, complete, abandonNonMatching, null);

    [Theory]
    [InlineData(true, true, true, nameof(ServiceBusSettlementAction.Complete))]
    [InlineData(true, false, true, nameof(ServiceBusSettlementAction.AcceptWithoutCompleting))]
    [InlineData(false, true, true, nameof(ServiceBusSettlementAction.Abandon))]
    [InlineData(false, true, false, nameof(ServiceBusSettlementAction.Ignore))]
    public void EachDeliveryIsSettledByTheWaitsRules(bool matches, bool complete, bool abandonNonMatching, string expected)
        => Assert.Equal(expected, ServiceBusSettlement.Decide(Request(complete, abandonNonMatching), matches).ToString());

    [Fact]
    public void ATransientErrorTheProcessorRetries_DoesNotEndTheWait()
        => Assert.False(ServiceBusSettlement.EndsTheWait(new ServiceBusException(true, "the link was dropped")));

    [Fact]
    public void ANonTransientServiceBusError_EndsTheWait()
        => Assert.True(ServiceBusSettlement.EndsTheWait(new ServiceBusException(false, "the entity does not exist")));

    [Fact]
    public void AnyOtherError_EndsTheWait()
        => Assert.True(ServiceBusSettlement.EndsTheWait(new UnauthorizedAccessException("no claim")));

    [Fact]
    public void EveryStatedFilterMustAccept()
    {
        ServiceBusReceiveRequest request = new("m-1", "c-1", _ => true, true);

        Assert.True(ServiceBusSettlement.Matches(request, "m-1", "c-1", predicate => true));
        Assert.False(ServiceBusSettlement.Matches(request, "m-2", "c-1", predicate => true));
        Assert.False(ServiceBusSettlement.Matches(request, "m-1", "c-2", predicate => true));
        Assert.False(ServiceBusSettlement.Matches(request, "m-1", "c-1", predicate => false));
    }

    [Fact]
    public void AWaitWithoutFilters_TakesTheFirstMessage()
        => Assert.True(ServiceBusSettlement.Matches(Request(true, false), "anything", null, predicate => throw new InvalidOperationException("no predicate was stated")));
}
