using System;
using System.Collections.Generic;
using TestFramework.Core.Exceptions;

namespace TestFramework.Azure.Exceptions;

/// <summary>
/// Thrown when a Function App host cannot be reached through the initial ping check or the HTTP reachability probe.
/// </summary>
/// <remarks>
/// A framework exception like every other this package throws, so it carries the steps that fix it and reads the same
/// way in a run's failure detail.
/// </remarks>
public sealed class FunctionAppPingException : TimelineFrameworkException
{
    internal FunctionAppPingException(string message, IReadOnlyList<string> recoverySteps, Exception? innerException)
        : base(message, recoverySteps, [], innerException)
    {
    }
}
