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
        (MethodInfo method, FunctionAttribute functionAttr) = GetMethodAndFunctionAttribute<TFunctionType>(methodName);

        foreach (ParameterInfo parameter in method.GetParameters())
        {
            Attribute? httpAttr = parameter.GetCustomAttributes()
                .FirstOrDefault(a => a.GetType().Name == "HttpTriggerAttribute");

            if (httpAttr is null) continue;

            Type attrType = httpAttr.GetType();
            string[]? methods = attrType.GetProperty("Methods")?.GetValue(httpAttr) as string[];
            string? route = attrType.GetProperty("Route")?.GetValue(httpAttr) as string;

            // Two declared methods for one call is a stated error, never array order: picking
            // methods[0] silently called a "get","post" trigger with GET whatever the author meant.
            // No declared methods means the trigger accepts them all, and GET is the canonical probe.
            if (methods is { Length: > 1 })
            {
                throw new TestFramework.Core.Exceptions.FrameworkConfigurationException(
                    $"Method '{methodName}' on '{typeof(TFunctionType).Name}' accepts {string.Join(", ", methods)}, so which one to call is ambiguous.",
                    ["State the verb explicitly with SelectEndpoint(path, method) or SelectFunction(name, method)."]);
            }

            HttpMethod httpMethod = methods is { Length: > 0 }
                ? new HttpMethod(methods[0])
                : HttpMethod.Get;

            // Routes are emitted RELATIVE to the host's route prefix. The prefix is applied once, by
            // the trigger, from FunctionAppConfig.RoutePrefix - a custom Route must not lose it, and a
            // missing Route must not hardcode it.
            string path = route ?? functionAttr.Name;

            return new TriggerHttpRouting(path, httpMethod, null!);
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
