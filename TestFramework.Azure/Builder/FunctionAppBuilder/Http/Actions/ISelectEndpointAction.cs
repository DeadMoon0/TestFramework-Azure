using System.Net.Http;
using TestFramework.Azure.Builder.FunctionAppBuilder.Http.Stages;
using TestFramework.Core.Variables;

namespace TestFramework.Azure.Builder.FunctionAppBuilder.Http.Actions;

/// <summary>
/// Selects a remote Function App endpoint by explicit path and HTTP method.
/// </summary>
public interface ISelectEndpointAction
{
    /// <summary>
    /// Selects a remote endpoint using an explicit path and HTTP method.
    /// </summary>
    /// <param name="subPath">The relative Function App route.</param>
    /// <param name="method">The HTTP method to use.</param>
    /// <returns>The next builder stage for adding payload details.</returns>
    public IFunctionAppHttpPayloadStage SelectEndpoint(VariableReference<string> subPath, VariableReference<HttpMethod> method);
}