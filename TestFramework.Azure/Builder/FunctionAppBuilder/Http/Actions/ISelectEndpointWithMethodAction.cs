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
}