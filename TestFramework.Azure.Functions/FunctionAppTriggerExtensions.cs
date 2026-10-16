using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using TestFramework.Azure.Builder.FunctionAppBuilder.Http;
using TestFramework.Azure.Builder.FunctionAppBuilder.Http.Stages;
using TestFramework.Azure.FunctionApp.Results;
using TestFramework.Azure.Functions.InProcess;
using TestFramework.Azure.Identifier;
using TestFramework.Core.Steps;

namespace TestFramework.Azure.Functions;

/// <summary>
/// The Function App triggers that need the function's own code: a managed call named by the function method's
/// attributes, and in-process HTTP execution.
/// </summary>
/// <remarks>
/// They extend <see cref="AzureExt.FunctionAppTrigger"/>, so the call reads <c>AzureExt.Trigger.FunctionApp.InProcessHttp&lt;T&gt;(...)</c>
/// as it does for the remote triggers.
/// </remarks>
public static class FunctionAppTriggerExtensions
{
    /// <summary>
    /// Calls a managed function named by the <c>[Function]</c> attribute on <paramref name="methodName"/>, and returns its
    /// managed result.
    /// </summary>
    /// <typeparam name="TFunction">The function host type containing the target method.</typeparam>
    /// <param name="trigger">The Function App trigger factory.</param>
    /// <param name="identifier">The Function App identifier to resolve.</param>
    /// <param name="methodName">The method marked with the Azure Functions trigger attributes.</param>
    /// <returns>A step that invokes the function and captures its result.</returns>
    /// <remarks>
    /// The method needs the isolated-worker <c>[Function]</c> attribute. In-process functions, marked
    /// <c>[FunctionName]</c>, are not analysed and are refused; call one by its name with
    /// <c>ManagedFunction(identifier, functionName)</c>, or reach an HTTP-triggered one over
    /// <c>Http(identifier).SelectFunction(name, method)</c>.
    /// </remarks>
    public static Step<ManagedResult> Managed<TFunction>(this AzureExt.FunctionAppTrigger trigger, FunctionAppIdentifier identifier, string methodName)
        where TFunction : notnull
    {
        ArgumentNullException.ThrowIfNull(trigger);
        return trigger.ManagedFunction(identifier, FunctionAppMethodAnalyzer.GetFunctionName<TFunction>(methodName));
    }

    /// <summary>
    /// Builds an in-process HTTP Function App trigger from a synchronous delegate.
    /// </summary>
    /// <typeparam name="TFunction">The function type to instantiate from DI.</typeparam>
    /// <param name="trigger">The Function App trigger factory.</param>
    /// <param name="action">The delegate that executes the in-process function logic.</param>
    /// <returns>The payload stage for building the request.</returns>
    public static IFunctionAppHttpPayloadStage InProcessHttp<TFunction>(this AzureExt.FunctionAppTrigger trigger, Action<TFunction, FunctionAppHttpInProcessCallProxy> action)
        where TFunction : notnull
    {
        ArgumentNullException.ThrowIfNull(action);
        return InProcess<TFunction>(trigger, (function, call) =>
        {
            action(function, call);
            return Task.FromResult<IActionResult?>(null);
        });
    }

    /// <summary>
    /// Builds an in-process HTTP Function App trigger from an asynchronous delegate.
    /// </summary>
    /// <typeparam name="TFunction">The function type to instantiate from DI.</typeparam>
    /// <param name="trigger">The Function App trigger factory.</param>
    /// <param name="action">The asynchronous delegate that executes the in-process function logic.</param>
    /// <returns>The payload stage for building the request.</returns>
    public static IFunctionAppHttpPayloadStage InProcessHttp<TFunction>(this AzureExt.FunctionAppTrigger trigger, Func<TFunction, FunctionAppHttpInProcessCallProxy, Task> action)
        where TFunction : notnull
    {
        ArgumentNullException.ThrowIfNull(action);
        return InProcess<TFunction>(trigger, async (function, call) =>
        {
            await action(function, call);
            return null;
        });
    }

    /// <summary>
    /// Builds an in-process HTTP Function App trigger from a synchronous delegate that returns an action result.
    /// </summary>
    /// <typeparam name="TFunction">The function type to instantiate from DI.</typeparam>
    /// <param name="trigger">The Function App trigger factory.</param>
    /// <param name="func">The delegate that executes the in-process function logic.</param>
    /// <returns>The payload stage for building the request.</returns>
    public static IFunctionAppHttpPayloadStage InProcessHttp<TFunction>(this AzureExt.FunctionAppTrigger trigger, Func<TFunction, FunctionAppHttpInProcessCallProxy, IActionResult> func)
        where TFunction : notnull
    {
        ArgumentNullException.ThrowIfNull(func);
        return InProcess<TFunction>(trigger, (function, call) => Task.FromResult<IActionResult?>(func(function, call)));
    }

    /// <summary>
    /// Builds an in-process HTTP Function App trigger from an asynchronous delegate that returns an action result.
    /// </summary>
    /// <typeparam name="TFunction">The function type to instantiate from DI.</typeparam>
    /// <param name="trigger">The Function App trigger factory.</param>
    /// <param name="func">The asynchronous delegate that executes the in-process function logic.</param>
    /// <returns>The payload stage for building the request.</returns>
    public static IFunctionAppHttpPayloadStage InProcessHttp<TFunction>(this AzureExt.FunctionAppTrigger trigger, Func<TFunction, FunctionAppHttpInProcessCallProxy, Task<IActionResult>> func)
        where TFunction : notnull
    {
        ArgumentNullException.ThrowIfNull(func);
        return InProcess<TFunction>(trigger, async (function, call) => await func(function, call));
    }

    // One shape for all four overloads: the request is composed by the same payload stage a remote call uses, and only
    // the step that receives it differs.
    private static FunctionAppHttpPayloadBuilder InProcess<TFunction>(AzureExt.FunctionAppTrigger trigger, Func<TFunction, FunctionAppHttpInProcessCallProxy, Task<IActionResult?>> action)
        where TFunction : notnull
    {
        ArgumentNullException.ThrowIfNull(trigger);
        return new FunctionAppHttpPayloadBuilder(request => new InProcessHttpFunctionAppTrigger<TFunction>(action, request));
    }
}
