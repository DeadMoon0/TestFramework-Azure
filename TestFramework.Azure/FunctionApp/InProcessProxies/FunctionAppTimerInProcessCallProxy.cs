using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using System;
using TestFramework.Core.Variables;

namespace TestFramework.Azure.FunctionApp.InProcessProxies;

public class FunctionAppTimerInProcessCallProxy
{
    private readonly IServiceProvider _serviceProvider;
    private readonly VariableStore _variableStore;

    public TimerInfo TimerInfo { get; }

    internal FunctionAppTimerInProcessCallProxy(TimerInfo timerInfo, IServiceProvider serviceProvider, VariableStore variableStore)
    {
        TimerInfo = timerInfo;
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