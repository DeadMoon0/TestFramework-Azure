using System.Net.Http;
using TestFramework.Azure.Builder.FunctionAppBuilder.Http.Stages;

namespace TestFramework.Azure.Builder.FunctionAppBuilder.Http.Actions;

/// <summary>
/// Selects a remote Function App endpoint from Azure Functions metadata on a method.
/// </summary>
public interface ISelectEndpointWithMethodAction
{
    /// <summary>
    /// Selects a remote Function App endpoint by reading its trigger metadata from the specified method.
    /// </summary>
    /// <typeparam name="TFunctionType">The type containing the Azure Function method.</typeparam>
    /// <param name="methodName">The method name to inspect.</param>
    /// <returns>The next builder stage for adding payload details.</returns>
    public IFunctionAppHttpPayloadStage SelectEndpointWithMethod<TFunctionType>(string methodName);

    /// <summary>
    /// Selects a remote Function App endpoint by reading its route from the specified method, calling it
    /// with the stated verb. Use this when the trigger declares more than one verb, which the single-argument
    /// overload refuses rather than guess at.
    /// </summary>
    /// <typeparam name="TFunctionType">The type containing the Azure Function method.</typeparam>
    /// <param name="methodName">The method name to inspect.</param>
    /// <param name="method">The verb to call. Must be one the trigger declares.</param>
    /// <returns>The next builder stage for adding payload details.</returns>
    public IFunctionAppHttpPayloadStage SelectEndpointWithMethod<TFunctionType>(string methodName, HttpMethod method);
}