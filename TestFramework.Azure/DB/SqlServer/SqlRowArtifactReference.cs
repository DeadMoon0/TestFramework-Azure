using TestFramework.Core.Steps;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Threading.Tasks;
using TestFramework.Azure.Identifier;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Variables;

namespace TestFramework.Azure.DB.SqlServer;

/// <summary>
/// Reference to a SQL row addressed by its primary key values.
/// </summary>
/// <typeparam name="TRow">The row entity type.</typeparam>
public class SqlRowArtifactReference<TRow> : ArtifactReference<SqlRowArtifactReference<TRow>, SqlRowArtifactDescriber<TRow>, SqlRowArtifactData<TRow>>
    where TRow : class
{
    private readonly VariableReference<string>[] _primaryKeyValues;
    private string[] _pinnedPKValues = [];

    /// <summary>
    /// The SQL database identifier used to resolve the backing <c>DbContext</c>.
    /// </summary>
    public SqlDatabaseIdentifier DbIdentifier { get; }

    /// <summary>
    /// Initializes a SQL row artifact reference.
    /// </summary>
    /// <param name="dbIdentifier">The SQL database identifier.</param>
    /// <param name="primaryKeyValues">Primary key values in entity key order.</param>
    public SqlRowArtifactReference(SqlDatabaseIdentifier dbIdentifier, params VariableReference<string>[] primaryKeyValues)
    {
        if (primaryKeyValues.Length == 0)
            throw new ArgumentException("At least one primary key value must be provided.", nameof(primaryKeyValues));

        DbIdentifier = dbIdentifier;
        _primaryKeyValues = primaryKeyValues;

        // A primary key is required above, so this reference can always address the row it deletes.
        // Whether it may delete it is the timeline's call, through MarkReadonly().
        CanDeconstruct = true;
    }

    /// <summary>
    /// Pins the reference to concrete primary key values.
    /// </summary>
    public override void OnPinReference(RunContext context)
    {
        _pinnedPKValues = _primaryKeyValues
            .Select(v => v.GetRequiredValue(context.Variables))
            .ToArray();
    }

    /// <summary>
    /// Declares the variable inputs required by the reference.
    /// </summary>
    public override void DeclareIO(StepIOContract contract)
    {
        foreach (var pkRef in _primaryKeyValues)
            if (pkRef.HasIdentifier)
                contract.Inputs.Add(new StepIOEntry(pkRef.Identifier!.Identifier, StepIOKind.Variable, true, typeof(string)));
    }

    internal string[] GetPrimaryKeyValues(VariableStore variableStore)
    {
        if (IsPinned || _pinnedPKValues.Length > 0) return _pinnedPKValues;
        return _primaryKeyValues.Select(v => v.GetRequiredValue(variableStore)).ToArray();
    }

    /// <summary>
    /// Pins this reference with pre-resolved PK string values.
    /// Used by <see cref="SqlEFCoreArtifactQueryFinder{TRow}"/> to create immediately-usable references
    /// without a live VariableStore (all PK values are known at query time).
    /// </summary>
    internal void PinWithValues(string[] values)
    {
        _pinnedPKValues = values;
    }

    /// <summary>
    /// Resolves the reference into concrete artifact data.
    /// </summary>
    public override async Task<ArtifactResolveResult<SqlRowArtifactDescriber<TRow>, SqlRowArtifactData<TRow>, SqlRowArtifactReference<TRow>>> ResolveToDataAsync(RunContext context, ArtifactVersionIdentifier versionIdentifier)
    {
        ISqlDbContextResolver resolver = context.Services.GetRequiredService<ISqlDbContextResolver>();
        await using DbContext dbContext = resolver.Resolve(context, DbIdentifier);
        await resolver.EnsureReadyAsync(dbContext, DbIdentifier, context.Logger);

        object[] keyValues = GetTypedKeyValues(dbContext, context.Variables);
        TRow? row = await dbContext.Set<TRow>().FindAsync(keyValues, context.Deadline.Token);

        if (row is null)
            return new ArtifactResolveResult<SqlRowArtifactDescriber<TRow>, SqlRowArtifactData<TRow>, SqlRowArtifactReference<TRow>> { Found = false };

        return new ArtifactResolveResult<SqlRowArtifactDescriber<TRow>, SqlRowArtifactData<TRow>, SqlRowArtifactReference<TRow>>
        {
            Found = true,
            Data = new SqlRowArtifactData<TRow>(row) { Identifier = versionIdentifier }
        };
    }

    /// <summary>
    /// Converts string PK values to the types expected by EF Core's key definition.
    /// Validates that the provided count matches the EF Core composite key count.
    /// </summary>
    internal object[] GetTypedKeyValues(DbContext dbContext, VariableStore variableStore)
    {
        string[] rawValues = GetPrimaryKeyValues(variableStore);

        var entityType = dbContext.Model.FindEntityType(typeof(TRow))
            ?? throw new TestFramework.Core.Exceptions.FrameworkConfigurationException($"Entity type '{typeof(TRow).Name}' is not registered in the DbContext.");

        var keyProperties = entityType.FindPrimaryKey()?.Properties
            ?? throw new TestFramework.Core.Exceptions.FrameworkConfigurationException($"No primary key defined for entity '{typeof(TRow).Name}'.");

        if (rawValues.Length != keyProperties.Count)
            throw new TestFramework.Core.Exceptions.FrameworkConfigurationException(
                $"Expected {keyProperties.Count} primary key value(s) for '{typeof(TRow).Name}' " +
                $"({string.Join(", ", keyProperties.Select(p => p.Name))}), " +
                $"but {rawValues.Length} were provided.");

        object[] keyValues = new object[rawValues.Length];
        for (int i = 0; i < rawValues.Length; i++)
        {
            Type clrType = keyProperties[i].ClrType;

            // Guid is not IConvertible, so Convert.ChangeType surfaced a raw InvalidCastException for
            // the most common non-primitive key type; anything else unconvertible refuses by name.
            try
            {
                keyValues[i] = clrType == typeof(Guid)
                    ? Guid.Parse(rawValues[i])
                    : Convert.ChangeType(rawValues[i], clrType);
            }
            catch (Exception exception) when (exception is InvalidCastException or FormatException or OverflowException)
            {
                throw new TestFramework.Core.Exceptions.FrameworkConfigurationException(
                    $"The primary key value '{rawValues[i]}' for '{typeof(TRow).Name}.{keyProperties[i].Name}' cannot be read as {clrType.Name}.");
            }
        }
        return keyValues;
    }

    /// <summary>
    /// Returns a readable string representation of the reference.
    /// </summary>
    /// <returns>A string representation of the reference.</returns>
    public override string ToString() => (IsPinned || _pinnedPKValues.Length > 0)
        ? $"SQL Row<{typeof(TRow).Name}>: {DbIdentifier}/({string.Join(", ", _pinnedPKValues)})"
        : $"SQL Row<{typeof(TRow).Name}> (unresolved)";
}
