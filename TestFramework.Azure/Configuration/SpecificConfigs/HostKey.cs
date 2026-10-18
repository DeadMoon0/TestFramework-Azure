namespace TestFramework.Azure.Configuration.SpecificConfigs;

/// <summary>
/// What a host key in configuration means when it is empty.
/// </summary>
/// <remarks>
/// An empty key in a settings file means "no key", and is held as one: two spellings of one state would leave
/// every reader to agree on it, and the one that did not sent an empty x-functions-key header. Function Apps and
/// Logic Apps share the rule, so they share this.
/// </remarks>
internal static class HostKey
{
    internal static string? NoneIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;
}
