using System;

namespace TestFramework.Azure.Exceptions;

public class ConfigurationValidationException(string property) : Exception($"{property} cannot be Empty or Null");