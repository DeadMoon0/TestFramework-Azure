using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System;
using TestFramework.Core.Variables;

namespace TestFramework.Azure.FunctionApp.InProcessProxies;

/// <summary>
/// Provides access to the current HTTP request, services, and timeline variables during an in-process Function App invocation.
/// </summary>
public class FunctionAppHttpInProcessCallProxy
{
    private readonly IServiceProvider _serviceProvider;
    private readonly VariableStore _variableStore;

    /// <summary>
    /// The current HTTP request being passed to the in-process function.
    /// </summary>
    public HttpRequest HttpRequest { get; }

    internal FunctionAppHttpInProcessCallProxy(HttpRequest httpRequest, IServiceProvider serviceProvider, VariableStore variableStore)
    {
        HttpRequest = httpRequest;
        _serviceProvider = serviceProvider;
        _variableStore = variableStore;
    }

    /// <summary>
    /// Resolves a service from the current dependency injection scope.
    /// </summary>
    /// <typeparam name="T">The service type to resolve.</typeparam>
    /// <returns>The resolved service instance.</returns>
    public T GetService<T>() where T : notnull
    {
        return _serviceProvider.GetRequiredService<T>();
    }

    /// <summary>
    /// Reads a timeline variable if it is available.
    /// </summary>
    /// <typeparam name="T">The variable value type.</typeparam>
    /// <param name="reference">The variable reference to inspect.</param>
    /// <returns>The variable value, or <see langword="null"/> when the reference has no value.</returns>
    public T? GetVariable<T>(VariableReference<T> reference)
    {
        return reference.GetValue(_variableStore);
    }
}