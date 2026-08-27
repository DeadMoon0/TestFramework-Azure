using TestFramework.Core.Steps;

namespace TestFramework.Azure.Runtime;

/// <summary>
/// The one door between "what is this resource configured as" and "give me a client for it".
/// </summary>
/// <remarks>
/// <para>
/// A client factory takes a <see cref="Resolved{TConfig}"/> and this class is the only thing that can make
/// one, so every client in the package is built from configuration the run stands behind. Without it,
/// handing a factory a record from anywhere else compiled and read like every neighbouring line - and for a
/// containerised resource returned a placeholder, which failed minutes later as a connection error rather
/// than as a message.
/// </para>
/// <para>
/// One method rather than one per resource type. There used to be five, each naming a kind and calling that
/// resource's own resolver; both of those disappeared when a run learned to answer for its own configuration,
/// because the shape that owns a record already knows the kind it belongs to. What is left is the minting -
/// which is the only part that was ever this class's job.
/// </para>
/// </remarks>
internal static class AzureResources
{
    /// <summary>
    /// The configuration of one resource, as this run knows it, in the form a client factory accepts.
    /// </summary>
    /// <typeparam name="TConfig">The configuration record.</typeparam>
    /// <param name="context">The run.</param>
    /// <param name="identifier">Which resource.</param>
    /// <returns>The record, resolved.</returns>
    internal static Resolved<TConfig> For<TConfig>(RunContext context, string identifier)
        where TConfig : class
        => new Minted<TConfig>(context.Configured<TConfig>(identifier));

    /// <summary>
    /// The only subclass, and private, so this class is the only source of a resolved record.
    /// </summary>
    /// <remarks>
    /// An enclosing type cannot reach a nested type's private constructor, which is why the token is an
    /// abstract base with a <c>private protected</c> constructor and this is a private subclass of it -
    /// the same shape the artifact write ticket uses in Core.
    /// </remarks>
    private sealed class Minted<TConfig>(TConfig value) : Resolved<TConfig>(value);
}
