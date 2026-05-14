using System.Net.Http;
using TestFramework.Azure.FunctionApp.Results;
using TestFramework.Core.Steps;

namespace TestFramework.Azure.Builder.FunctionAppBuilder.Http.Actions;

#pragma warning disable CA1716 // Call() method name matches reserved keyword
/// <summary>
/// Final builder action that invokes a remote Function App HTTP request.
/// </summary>
public interface ICallRemoteHttpAction
{
    /// <summary>
    /// Builds the current request into an executable timeline step.
    /// </summary>
    /// <returns>A step that performs the HTTP call.</returns>
    public Step<HttpResponseResultContext> Call();
}
#pragma warning restore CA1716