using System;

namespace TestFramework.Azure.Runtime;

/// <summary>
/// A configuration record whose coordinates came from the run, not straight out of a declaration.
/// </summary>
/// <remarks>
/// <para>
/// A client factory needs a connection string, and a connection string is exactly the value that a
/// configuration file cannot hold for a container - the port is chosen when it starts. So there are two
/// records of the same type in play: the one somebody wrote down, which may be a placeholder, and the one
/// the run resolved. Passing the wrong one compiles, reads like every neighbouring line, and fails minutes
/// later as a connection error against a default port.
/// </para>
/// <para>
/// That happened, twice in one file, while three sibling cases in the same file had already been migrated -
/// which is the argument for a type rather than a rule. Reading a declaration stays perfectly legal; what
/// is no longer expressible is handing one to a client.
/// </para>
/// <para>
/// Unforgeable in the shape the guardrails document names for this: the constructor is
/// <c>private protected</c>, and the only subclass is a private nested type of
/// <see cref="AzureResources"/>. So the single way to obtain one is to ask that class, and that class always
/// resolves through the run.
/// </para>
/// </remarks>
/// <typeparam name="TConfig">The configuration record.</typeparam>
internal abstract class Resolved<TConfig>
{
    private protected Resolved(TConfig value)
    {
        ArgumentNullException.ThrowIfNull(value);

        this.Value = value;
    }

    /// <summary>
    /// The record itself, for the properties a caller reads directly.
    /// </summary>
    internal TConfig Value { get; }

    /// <summary>
    /// Names the record rather than printing it, because these carry connection strings.
    /// </summary>
    /// <returns>The type's name.</returns>
    public override string ToString() => $"resolved {typeof(TConfig).Name}";
}
