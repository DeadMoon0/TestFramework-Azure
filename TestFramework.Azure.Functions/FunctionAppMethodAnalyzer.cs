using Microsoft.Azure.Functions.Worker;
using System;
using System.Linq;
using System.Net.Http;
using System.Reflection;

namespace TestFramework.Azure.Functions;

internal static class FunctionAppMethodAnalyzer
{
    internal static string GetFunctionName<TFunctionType>(string methodName)
    {
        (_, FunctionAttribute functionAttr) = GetMethodAndFunctionAttribute<TFunctionType>(methodName);
        return functionAttr.Name;
    }

    internal static FunctionAppHttpEndpoint GetHttpEndpoint<TFunctionType>(string methodName)
    {
        HttpTriggerMetadata trigger = GetHttpTriggerMetadata<TFunctionType>(methodName);

        // Two declared methods for one call is a stated error, never array order: picking
        // methods[0] silently called a "get","post" trigger with GET whatever the author meant.
        // No declared methods means the trigger accepts them all, and GET is the canonical probe.
        if (trigger.Methods is { Length: > 1 })
        {
            throw new TestFramework.Core.Exceptions.FrameworkConfigurationException(
                $"Method '{methodName}' on '{typeof(TFunctionType).Name}' accepts {string.Join(", ", trigger.Methods)}, so which one to call is ambiguous.")
            {
                RecoverySteps = ["State the verb explicitly with SelectEndpointWithMethod(methodName, method), SelectEndpoint(path, method) or SelectFunction(name, method)."],
            };
        }

        HttpMethod httpMethod = trigger.Methods is { Length: > 0 }
            ? new HttpMethod(trigger.Methods[0])
            : HttpMethod.Get;

        return new FunctionAppHttpEndpoint(trigger.Path, httpMethod);
    }

    internal static FunctionAppHttpEndpoint GetHttpEndpoint<TFunctionType>(string methodName, HttpMethod httpMethod)
    {
        HttpTriggerMetadata trigger = GetHttpTriggerMetadata<TFunctionType>(methodName);

        // The route still comes from the metadata; only the verb is stated. A verb the trigger does
        // not declare is refused here rather than sent, because the function would reject it at run
        // time as a 404 that says nothing about the real mistake. No declared methods accepts all.
        if (trigger.Methods is { Length: > 0 }
            && !trigger.Methods.Any(m => string.Equals(m, httpMethod.Method, StringComparison.OrdinalIgnoreCase)))
        {
            throw new TestFramework.Core.Exceptions.FrameworkConfigurationException(
                $"Method '{methodName}' on '{typeof(TFunctionType).Name}' accepts {string.Join(", ", trigger.Methods)}, not {httpMethod.Method}.")
            {
                RecoverySteps = ["State one of the verbs the trigger declares, or widen the [HttpTrigger] attribute."],
            };
        }

        return new FunctionAppHttpEndpoint(trigger.Path, httpMethod);
    }

    private readonly record struct HttpTriggerMetadata(string Path, string[]? Methods);

    private static HttpTriggerMetadata GetHttpTriggerMetadata<TFunctionType>(string methodName)
    {
        (MethodInfo method, FunctionAttribute functionAttr) = GetMethodAndFunctionAttribute<TFunctionType>(methodName);

        foreach (ParameterInfo parameter in method.GetParameters())
        {
            HttpTriggerAttribute? httpAttr = parameter.GetCustomAttribute<HttpTriggerAttribute>();

            if (httpAttr is null) continue;

            // Routes are emitted RELATIVE to the host's route prefix. The prefix is applied once, by
            // the trigger, from FunctionAppConfig.RoutePrefix - a custom Route must not lose it, and a
            // missing Route must not hardcode it.
            return new HttpTriggerMetadata(httpAttr.Route ?? functionAttr.Name, httpAttr.Methods);
        }

        throw new TestFramework.Core.Exceptions.FrameworkConfigurationException(
            $"Method '{methodName}' on '{typeof(TFunctionType).Name}' has no parameter with [HttpTrigger] attribute.");
    }

    /// <summary>
    /// Finds the method and its isolated-worker <see cref="FunctionAttribute"/>.
    /// </summary>
    /// <remarks>
    /// Only the isolated-worker <c>[Function]</c> attribute is read. An in-process function, marked
    /// <c>[FunctionName]</c>, is not analysed and is refused here as missing <c>[Function]</c>: call it over HTTP
    /// with <c>SelectFunction(name, method)</c> instead.
    /// </remarks>
    private static (MethodInfo method, FunctionAttribute functionAttr) GetMethodAndFunctionAttribute<TFunctionType>(string methodName)
    {
        MethodInfo method = typeof(TFunctionType).GetMethod(methodName)
            ?? throw new TestFramework.Core.Exceptions.FrameworkConfigurationException(
                $"Method '{methodName}' not found on type '{typeof(TFunctionType).Name}'.");

        FunctionAttribute functionAttr = method.GetCustomAttribute<FunctionAttribute>()
            ?? throw new TestFramework.Core.Exceptions.FrameworkConfigurationException(
                $"Method '{methodName}' on '{typeof(TFunctionType).Name}' is missing the [Function] attribute.");

        return (method, functionAttr);
    }
}
