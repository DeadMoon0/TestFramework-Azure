using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using TestFramework.Azure.FunctionApp.InProcessProxies;

namespace TestFramework.Azure.FunctionApp.TriggerConfigs;

internal record HttpInProcessTriggerConfig(Type FunctionType, Func<object, FunctionAppHttpInProcessCallProxy, Task<IActionResult>> InvocationAction);
internal record TimerInProcessTriggerConfig(Type FunctionType, Func<object, FunctionAppTimerInProcessCallProxy, Task> InvocationAction);