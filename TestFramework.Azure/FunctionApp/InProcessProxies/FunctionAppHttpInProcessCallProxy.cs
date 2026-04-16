using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System;
using TestFramework.Core.Variables;

namespace TestFramework.Azure.FunctionApp.InProcessProxies;

public class FunctionAppHttpInProcessCallProxy
{
    private readonly IServiceProvider _serviceProvider;
    private readonly VariableStore _variableStore;

    public HttpRequest HttpRequest { get; }

    internal FunctionAppHttpInProcessCallProxy(HttpRequest httpRequest, IServiceProvider serviceProvider, VariableStore variableStore)
    {
        HttpRequest = httpRequest;
        _serviceProvider = serviceProvider;
        _variableStore = variableStore;
    }

    public T GetService<T>() where T : notnull
    {
        return _serviceProvider.GetRequiredService<T>();
    }

    public T? GetVariable<T>(VariableReference<T> reference)
    {
        return reference.GetValue(_variableStore);
    }
}