using TestFramework.Azure.LogicApp;
using Xunit;

namespace TestFramework.Azure.Tests;

/// <summary>
/// Reading what a Logic App runtime answers: where to call it back, and how far a run has got.
/// </summary>
public class LogicAppJsonTests
{
    [Fact]
    public void ALocalCallbackOnTheRuntimesOwnPort_IsRebasedOntoTheAddressTheTestReaches()
    {
        // A containerised runtime names the port it listens on inside; the test reaches it through a mapped one.
        string rebased = LogicAppJson.RebaseLocalCallbackUrl(
            "http://localhost:49153/",
            "http://localhost:7071/api/orders/triggers/manual/invoke?api-version=2022-05-01&sig=abc");

        Assert.Equal("http://localhost:49153/api/orders/triggers/manual/invoke?api-version=2022-05-01&sig=abc", rebased);
    }

    [Fact]
    public void ACallbackOnARealHost_IsLeftAlone()
    {
        const string callback = "https://prod-12.westeurope.logic.azure.com/workflows/abc/triggers/manual/paths/invoke?sig=xyz";

        Assert.Equal(callback, LogicAppJson.RebaseLocalCallbackUrl("http://localhost:49153/", callback));
    }

    [Fact]
    public void TheCallbackIsReadFromEitherShapeTheRuntimesAnswerWith()
    {
        Assert.Equal("http://a/", LogicAppJson.ExtractCallbackUrl("""{ "value": "http://a/" }"""));
        Assert.Equal("http://b/", LogicAppJson.ExtractCallbackUrl("""{ "callbackUrl": "http://b/" }"""));
        Assert.False(LogicAppJson.TryExtractCallbackUrl("""{ "other": 1 }""", out _));
        Assert.False(LogicAppJson.TryExtractCallbackUrl("not json", out _));
    }

    [Fact]
    public void ARunStillRunningWhoseResponseHasFinished_CountsAsFinished()
    {
        // A workflow that answered its caller is done as far as the caller is concerned, even while the run
        // record still says Running.
        LogicAppRunDetails details = LogicAppJson.ParseRunDetails(
            "orders",
            "run-1",
            """{ "properties": { "status": "Running", "response": { "status": "Succeeded", "code": "OK" } } }""");

        Assert.Equal(LogicAppRunStatus.Succeeded, details.Status);
        Assert.Equal("OK", details.Code);
    }

    [Fact]
    public void ARunIsFoundInARunsListByTheLastSegmentOfItsId()
    {
        LogicAppRunDetails? details = LogicAppJson.TryParseRunDetailsFromRunsList(
            "orders",
            "RUN-2",
            """
            { "value": [
                { "id": "/workflows/orders/runs/run-1", "properties": { "status": "Failed" } },
                { "id": "/workflows/orders/runs/run-2", "properties": { "status": "Succeeded" } }
            ] }
            """);

        Assert.NotNull(details);
        Assert.Equal(LogicAppRunStatus.Succeeded, details.Status);
    }

    [Theory]
    [InlineData("Succeeded", LogicAppRunStatus.Succeeded)]
    [InlineData(" timedout ", LogicAppRunStatus.TimedOut)]
    [InlineData("Paused", LogicAppRunStatus.Unknown)]
    [InlineData(null, LogicAppRunStatus.Unknown)]
    public void AStatusIsReadRegardlessOfCase_AndAnUnknownOneStaysUnknown(string? text, LogicAppRunStatus expected)
        => Assert.Equal(expected, LogicAppJson.ParseStatus(text));
}
