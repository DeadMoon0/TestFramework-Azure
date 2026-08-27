using System;
using System.Collections.Generic;
using TestFramework.Core.Exceptions;
using System.Globalization;
using Microsoft.Extensions.Configuration;
using TestFramework.Azure.Configuration.SpecificConfigs;
using TestFramework.Config.Configuration;
using TestFramework.Core.Environment.Graph;

namespace TestFramework.Azure.Configuration;

/// <summary>
/// The SQL database section, as the run's resource graph sees it.
/// </summary>
/// <remarks>
/// <para>
/// A configured SQL database is a resource somebody declared, and this says which values of the graph's an entry
/// actually holds. What it buys is that a reader asks the run where something is and never learns whether a
/// person wrote it down or something published it while starting.
/// </para>
/// <para>
/// Reading delegates to the package's own provider rather than binding the section again here: two readers for
/// one section is two sets of validation rules, and the second one written is the one that quietly accepts
/// what the first refuses.
/// </para>
/// The context type an entry may name is not published: it says which EF model to use, which is a fact about
/// the test's own code rather than about where the database is.
/// </remarks>
/// <param name="provider">Reads an entry, including whatever validation the caller configured.</param>
internal sealed class SqlDatabaseShape(IConfigProvider provider) : ConfigShape<SqlDatabaseConfig>
{
    /// <inheritdoc />
    public override string Section => DefaultConfigProvider.SqlDatabaseSelector;

    /// <inheritdoc />
    public override ResourceKind Kind => AzureEnvironmentResourceKinds.SqlKind;

    /// <inheritdoc />
    public override IReadOnlyList<string> Identifiers(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return provider.LoadAllSqlDatabaseIdentifier(configuration);
    }

    /// <inheritdoc />
    public override SqlDatabaseConfig Read(IConfiguration configuration, string identifier)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return provider.LoadSqlDatabaseConfig(configuration, identifier);
    }

    /// <inheritdoc />
    public override IReadOnlyDictionary<ValueKey, string> Values(SqlDatabaseConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        Dictionary<ValueKey, string> values = [];

        if (config.ConnectionString is { Length: > 0 } connectionString)
        {
            values[new ValueKey(ValueNames.ConnectionString, ResourceVantage.Host)] = connectionString;
            values[new ValueKey(ValueNames.ConnectionString, ResourceVantage.Network)] = connectionString;
        }

        if (config.DatabaseName is { Length: > 0 } databaseName)
        {
            values[new ValueKey(AzureEnvironmentResourceKinds.DatabaseNameValue)] = databaseName;
        }

        return values;
    }

    /// <inheritdoc />
    public override SqlDatabaseConfig Read(IReadOnlyDictionary<string, string> values, string identifier)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);

        return new SqlDatabaseConfig
        {
            ConnectionString = Text(values, ValueNames.ConnectionString) ?? throw Missing(ValueNames.ConnectionString, identifier, values),
            DatabaseName = Text(values, AzureEnvironmentResourceKinds.DatabaseNameValue) ?? throw Missing(AzureEnvironmentResourceKinds.DatabaseNameValue, identifier, values),
        };
    }

    private static string? Text(IReadOnlyDictionary<string, string> values, string valueName)
        => values.TryGetValue(valueName, out string? value) && value is { Length: > 0 } ? value : null;

    private static bool Flag(IReadOnlyDictionary<string, string> values, string valueName, string identifier, bool fallback)
    {
        if (!values.TryGetValue(valueName, out string? value))
        {
            return fallback;
        }

        return bool.TryParse(value, out bool parsed)
            ? parsed
            : throw Unreadable(value, identifier, valueName, "'true' or 'false'");
    }

    private static FrameworkConfigurationException Unreadable(string value, string identifier, string valueName, string expected)
        => new FrameworkConfigurationException(
            $"The run holds '{value}' as {valueName} for '{identifier}', which cannot be read as {expected}.",
            [
                $"Correct the configuration entry, or the environment component that publishes it.",
            ]);

    private static FrameworkConfigurationException Missing(string valueName, string identifier, IReadOnlyDictionary<string, string> values)
        => new FrameworkConfigurationException(
            $"The run has no {valueName} for '{identifier}'.",
            [
                "Add it to the configuration entry, or include the definition that provisions the resource.",
            ],
            [.. values.Keys]);
}
