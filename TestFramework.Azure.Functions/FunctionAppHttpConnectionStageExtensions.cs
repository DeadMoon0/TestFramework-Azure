using System;
using System.Net.Http;
using TestFramework.Azure.Builder.FunctionAppBuilder.Http.Stages;
using TestFramework.Core.Variables;

namespace TestFramework.Azure.Functions;

/// <summary>
/// Selects a remote Function App endpoint from the Azure Functions attributes on a method.
/// </summary>
/// <remarks>
/// The method needs the isolated-worker <c>[Function]</c> attribute and a parameter marked <c>[HttpTrigger]</c>.
/// In-process functions, marked <c>[FunctionName]</c>, are not analysed and are refused; select those with
/// <c>SelectFunction(name, method)</c>. The endpoint is read when the call is built, so a missing attribute fails there
/// rather than in the run.
/// </remarks>
public static class FunctionAppHttpConnectionStageExtensions
{
    /// <summary>
    /// Selects a remote Function App endpoint by reading its route and verb from the specified method.
    /// </summary>
    /// <typeparam name="TFunctionType">The type containing the Azure Function method.</typeparam>
    /// <param name="stage">The connection stage of a remote Function App HTTP call.</param>
    /// <param name="methodName">The method name to inspect.</param>
    /// <returns>The next builder stage for adding payload details.</returns>
    /// <remarks>
    /// A trigger that declares more than one verb is refused rather than guessed at; state the verb with the
    /// <see cref="SelectEndpointWithMethod{TFunctionType}(IFunctionAppHttpConnectionStage, string, HttpMethod)"/> overload.
    /// </remarks>
    public static IFunctionAppHttpPayloadStage SelectEndpointWithMethod<TFunctionType>(this IFunctionAppHttpConnectionStage stage, string methodName)
    {
        ArgumentNullException.ThrowIfNull(stage);
        return SelectEndpoint(stage, FunctionAppMethodAnalyzer.GetHttpEndpoint<TFunctionType>(methodName));
    }

    /// <summary>
    /// Selects a remote Function App endpoint by reading its route from the specified method, calling it with the
    /// stated verb.
    /// </summary>
    /// <typeparam name="TFunctionType">The type containing the Azure Function method.</typeparam>
    /// <param name="stage">The connection stage of a remote Function App HTTP call.</param>
    /// <param name="methodName">The method name to inspect.</param>
    /// <param name="method">The verb to call. Must be one the trigger declares.</param>
    /// <returns>The next builder stage for adding payload details.</returns>
    public static IFunctionAppHttpPayloadStage SelectEndpointWithMethod<TFunctionType>(this IFunctionAppHttpConnectionStage stage, string methodName, HttpMethod method)
    {
        ArgumentNullException.ThrowIfNull(stage);
        ArgumentNullException.ThrowIfNull(method);
        return SelectEndpoint(stage, FunctionAppMethodAnalyzer.GetHttpEndpoint<TFunctionType>(methodName, method));
    }

    private static IFunctionAppHttpPayloadStage SelectEndpoint(IFunctionAppHttpConnectionStage stage, FunctionAppHttpEndpoint endpoint) =>
        stage.SelectEndpoint(Var.Const(endpoint.Path), Var.Const(endpoint.Method));
}
