using System;
using System.Linq;
using System.Reflection;
using System.Text;
using TestFramework.Azure.LogicApp;

namespace TestFramework.Azure.Configuration.SpecificConfigs;

/// <summary>
/// Marks a configuration property whose value is a credential, so a record's text says it is set without saying it.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
internal sealed class SecretAttribute : Attribute;

/// <summary>
/// Marks a configuration property holding a URL whose query carries a shared-access signature: the URL is shown
/// with the signature redacted.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
internal sealed class SignedUrlAttribute : Attribute;

/// <summary>
/// Writes a configuration record's members for its <c>ToString()</c>, with its credentials redacted.
/// </summary>
/// <remarks>
/// <para>
/// A record's synthesized text prints every member, so a connection string reached a log line, an assertion message
/// or a debugger's value view the moment the record did. Every configuration record writes its members through this
/// instead: one place decides what a credential looks like in text, so a secret added to a record later is redacted by
/// marking it, not by remembering to edit a hand-written printer.
/// </para>
/// <para>
/// Only the record's data is written - properties that can be set. A computed member such as
/// <c>BlobContainerNameRequired</c> throws when its value is absent, which used to make the record's own
/// <c>ToString()</c> throw.
/// </para>
/// </remarks>
internal static class ConfigRecordText
{
    internal const string Hidden = "(secret)";

    /// <summary>
    /// Writes the record's members, the way a record's synthesized <c>PrintMembers</c> does.
    /// </summary>
    /// <param name="record">The configuration record.</param>
    /// <param name="builder">The text being built.</param>
    /// <returns>Whether anything was written.</returns>
    internal static bool PrintMembers(object record, StringBuilder builder)
    {
        PropertyInfo[] members = [.. record.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.CanRead && property.SetMethod is { IsPublic: true })
            .OrderBy(property => property.MetadataToken)];

        for (int index = 0; index < members.Length; index++)
        {
            if (index > 0)
                builder.Append(", ");

            builder.Append(members[index].Name).Append(" = ").Append(TextOf(members[index], members[index].GetValue(record)));
        }

        return members.Length > 0;
    }

    private static string TextOf(PropertyInfo property, object? value)
    {
        if (value is null)
            return "null";

        if (property.IsDefined(typeof(SecretAttribute)))
            return Hidden;

        if (property.IsDefined(typeof(SignedUrlAttribute)))
            return LogicAppUriRedaction.Redact(value.ToString()) ?? string.Empty;

        return value.ToString() ?? string.Empty;
    }
}
