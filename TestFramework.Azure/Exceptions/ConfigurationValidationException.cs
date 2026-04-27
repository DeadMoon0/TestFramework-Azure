using System;

namespace TestFramework.Azure.Exceptions;

/// <summary>
/// Thrown when a required Azure configuration value is missing.
/// </summary>
/// <param name="property">The missing property name.</param>
public class ConfigurationValidationException(string property) : Exception($"{property} cannot be Empty or Null");