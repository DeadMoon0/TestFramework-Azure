using System;

namespace TestFramework.Azure.Exceptions;

public class FunctionAppPingException(string? message, Exception? innerException) : Exception(message, innerException)
{
}