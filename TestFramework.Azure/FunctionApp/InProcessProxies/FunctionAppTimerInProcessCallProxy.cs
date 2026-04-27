using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using System;
using TestFramework.Core.Variables;

namespace TestFramework.Azure.FunctionApp.InProcessProxies;

/// <summary>
/// Provides access to the current timer trigger payload, services, and timeline variables during an in-process Function App invocation.
/// </summary>
public class FunctionAppTimerInProcessCallProxy
{
    private readonly IServiceProvider _serviceProvider;
    private readonly VariableStore _variableStore;

    /// <summary>
    /// The current Azure Functions timer payload.
    /// </summary>
    public TimerInfo TimerInfo { get; }

    internal FunctionAppTimerInProcessCallProxy(TimerInfo timerInfo, IServiceProvider serviceProvider, VariableStore variableStore)
    {
        TimerInfo = timerInfo;
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