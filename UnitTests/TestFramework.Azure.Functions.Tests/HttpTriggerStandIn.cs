using System;

namespace TestFramework.Azure.Functions.Tests;

// The Worker's [HttpTrigger] ships in its HTTP extension package, which a function project brings and this suite does
// not. The endpoint selection reads the attribute by name and only its Methods and Route, so a stand-in of the same
// shape is what it sees in a real function project.

public enum AuthorizationLevel
{
    Function,
}

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class HttpTriggerAttribute(AuthorizationLevel authLevel, params string[] methods) : Attribute
{
    public AuthorizationLevel AuthorizationLevel { get; } = authLevel;

    public string[] Methods { get; } = methods;

    public string? Route { get; init; }
}
