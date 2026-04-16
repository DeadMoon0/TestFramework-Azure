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

            HttpMethod httpMethod = methods is { Length: > 0 }
                ? new HttpMethod(methods[0])
                : HttpMethod.Get;

            string path = route ?? $"api/{functionAttr.Name}";

            return new TriggerHttpRouting(path, httpMethod, null!);
        }

        throw new InvalidOperationException(
            $"Method '{methodName}' on '{typeof(TFunctionType).Name}' has no parameter with [HttpTrigger] attribute.");
    }

    private static (MethodInfo method, FunctionAttribute functionAttr) GetMethodAndFunctionAttribute<TFunctionType>(string methodName)
    {
        MethodInfo method = typeof(TFunctionType).GetMethod(methodName)
            ?? throw new InvalidOperationException(
                $"Method '{methodName}' not found on type '{typeof(TFunctionType).Name}'.");

        FunctionAttribute functionAttr = method.GetCustomAttribute<FunctionAttribute>()
            ?? throw new InvalidOperationException(
                $"Method '{methodName}' on '{typeof(TFunctionType).Name}' is missing the [Function] attribute.");

        return (method, functionAttr);
    }
}
