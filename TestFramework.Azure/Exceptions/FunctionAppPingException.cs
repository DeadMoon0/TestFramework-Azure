using System;

namespace TestFramework.Azure.Exceptions;

/// <summary>
/// Thrown when a Function App host cannot be reached through the initial ping check.
/// </summary>
/// <param name="message">The error message.</param>
/// <param name="innerException">The underlying exception, if any.</param>
public class FunctionAppPingException(string? message, Exception? innerException) : Exception(message, innerException)
{
}