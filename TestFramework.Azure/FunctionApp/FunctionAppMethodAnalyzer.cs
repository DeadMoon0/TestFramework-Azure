using Microsoft.Azure.Functions.Worker;
using System;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using TestFramework.Azure.FunctionApp.TriggerConfigs;

namespace TestFramework.Azure.FunctionApp;

internal static class FunctionAppMethodAnalyzer
{
    internal static TriggerRouting GetTriggerRouting<TFunctionType>(string methodName)
    {
        (_, FunctionAttribute functionAttr) = GetMethodAndFunctionAttribute<TFunctionType>(methodName);
        return new TriggerRouting(functionAttr.Name);
    }

    internal static TriggerHttpRouting GetHttpTriggerRouting<TFunctionType>(string methodName)
    {
        HttpTriggerMetadata trigger = GetHttpTriggerMetadata<TFunctionType>(methodName);

        // Two declared methods for one call is a stated error, never array order: picking
        // methods[0] silently called a "get","post" trigger with GET whatever the author meant.
        // No declared methods means the trigger accepts them all, and GET is the canonical probe.
        if (trigger.Methods is { Length: > 1 })
        {
            throw new TestFramework.Core.Exceptions.FrameworkConfigurationException(
                $"Method '{methodName}' on '{typeof(TFunctionType).Name}' accepts {string.Join(", ", trigger.Methods)}, so which one to call is ambiguous.",
                ["State the verb explicitly with SelectEndpointWithMethod(methodName, method), SelectEndpoint(path, method) or SelectFunction(name, method)."]);
        }

        HttpMethod httpMethod = trigger.Methods is { Length: > 0 }
            ? new HttpMethod(trigger.Methods[0])
            : HttpMethod.Get;

        return new TriggerHttpRouting(trigger.Path, httpMethod, null!);
    }

    internal static TriggerHttpRouting GetHttpTriggerRouting<TFunctionType>(string methodName, HttpMethod httpMethod)
    {
        HttpTriggerMetadata trigger = GetHttpTriggerMetadata<TFunctionType>(methodName);

        // The route still comes from the metadata; only the verb is stated. A verb the trigger does
        // not declare is refused here rather than sent, because the function would reject it at run
        // time as a 404 that says nothing about the real mistake. No declared methods accepts all.
        if (trigger.Methods is { Length: > 0 }
            && !trigger.Methods.Any(m => string.Equals(m, httpMethod.Method, StringComparison.OrdinalIgnoreCase)))
        {
            throw new TestFramework.Core.Exceptions.FrameworkConfigurationException(
                $"Method '{methodName}' on '{typeof(TFunctionType).Name}' accepts {string.Join(", ", trigger.Methods)}, not {httpMethod.Method}.",
                ["State one of the verbs the trigger declares, or widen the [HttpTrigger] attribute."]);
        }

        return new TriggerHttpRouting(trigger.Path, httpMethod, null!);
    }

    private readonly record struct HttpTriggerMetadata(string Path, string[]? Methods);

    private static HttpTriggerMetadata GetHttpTriggerMetadata<TFunctionType>(string methodName)
    {
        (MethodInfo method, FunctionAttribute functionAttr) = GetMethodAndFunctionAttribute<TFunctionType>(methodName);

        foreach (ParameterInfo parameter in method.GetParameters())
        {
            Attribute? httpAttr = parameter.GetCustomAttributes()
                .FirstOrDefault(a => a.GetType().Name == "HttpTriggerAttribute");

            if (httpAttr is null) continue;

            Type attrType = httpAttr.GetType();
            string[]? methods = attrType.GetProperty("Methods")?.GetValue(httpAttr) as string[];
            string? route = attrType.GetProperty("Route")?.GetValue(httpAttr) as string;

            // Routes are emitted RELATIVE to the host's route prefix. The prefix is applied once, by
            // the trigger, from FunctionAppConfig.RoutePrefix - a custom Route must not lose it, and a
            // missing Route must not hardcode it.
            return new HttpTriggerMetadata(route ?? functionAttr.Name, methods);
        }

        throw new TestFramework.Core.Exceptions.FrameworkConfigurationException(
            $"Method '{methodName}' on '{typeof(TFunctionType).Name}' has no parameter with [HttpTrigger] attribute.");
    }

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
