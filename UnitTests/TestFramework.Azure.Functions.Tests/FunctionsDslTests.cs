using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using System.Threading.Tasks;
using TestFramework.Azure.FunctionApp.Results;
using TestFramework.Core.Steps;

namespace TestFramework.Azure.Functions.Tests;

/// <summary>
/// The calls this package adds read as they did on <c>AzureExt.Trigger.FunctionApp</c> before it was split out: the
/// same names, the same overloads, chosen by the delegate's shape.
/// </summary>
public class FunctionsDslTests
{
    [Fact]
    public void Managed_BuildsAStep()
    {
        Step<ManagedResult> step = AzureExt.Trigger.FunctionApp.Managed<DummyFunction>("func", nameof(DummyFunction.Run));

        Assert.NotNull(step);
    }

    [Fact]
    public void InProcessHttp_TakesEachDelegateShape()
    {
        object actionBuilder = AzureExt.Trigger.FunctionApp.InProcessHttp<DummyFunction>((_, _) => { });
        object taskBuilder = AzureExt.Trigger.FunctionApp.InProcessHttp<DummyFunction>((_, _) => Task.CompletedTask);
        object resultBuilder = AzureExt.Trigger.FunctionApp.InProcessHttp<DummyFunction>((_, _) => new OkResult());
        object taskResultBuilder = AzureExt.Trigger.FunctionApp.InProcessHttp<DummyFunction>((_, _) => Task.FromResult<IActionResult>(new OkResult()));

        Assert.NotNull(actionBuilder);
        Assert.NotNull(taskBuilder);
        Assert.NotNull(resultBuilder);
        Assert.NotNull(taskResultBuilder);
    }

    private sealed class DummyFunction
    {
        [Function("Run")]
        public void Run()
        {
        }
    }
}
