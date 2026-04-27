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
        CanDeconstruct = false;
    }

    /// <summary>
    /// Pins the reference to concrete primary key values.
    /// </summary>
    public override void OnPinReference(VariableStore variableStore, ScopedLogger logger)
    {
        _pinnedPKValues = _primaryKeyValues
            .Select(v => v.GetRequiredValue(variableStore))
            .ToArray();
        CanDeconstruct = true;
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
        CanDeconstruct = true;
    }

    /// <summary>
    /// Resolves the reference into concrete artifact data.
    /// </summary>
    public override async Task<ArtifactResolveResult<SqlRowArtifactDescriber<TRow>, SqlRowArtifactData<TRow>, SqlRowArtifactReference<TRow>>> ResolveToDataAsync(
        IServiceProvider serviceProvider, ArtifactVersionIdentifier versionIdentifier, VariableStore variableStore, ScopedLogger logger)
    {
        ISqlDbContextResolver resolver = serviceProvider.GetRequiredService<ISqlDbContextResolver>();
        DbContext context = resolver.Resolve(DbIdentifier);
        await resolver.EnsureReadyAsync(context, DbIdentifier, logger);

        object[] keyValues = GetTypedKeyValues(context, variableStore);
        TRow? row = await context.Set<TRow>().FindAsync(keyValues);

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
    internal object[] GetTypedKeyValues(DbContext context, VariableStore variableStore)
    {
        string[] rawValues = GetPrimaryKeyValues(variableStore);

        var entityType = context.Model.FindEntityType(typeof(TRow))
            ?? throw new InvalidOperationException($"Entity type '{typeof(TRow).Name}' is not registered in the DbContext.");

        var keyProperties = entityType.FindPrimaryKey()?.Properties
            ?? throw new InvalidOperationException($"No primary key defined for entity '{typeof(TRow).Name}'.");

        if (rawValues.Length != keyProperties.Count)
            throw new InvalidOperationException(
                $"Expected {keyProperties.Count} primary key value(s) for '{typeof(TRow).Name}' " +
                $"({string.Join(", ", keyProperties.Select(p => p.Name))}), " +
                $"but {rawValues.Length} were provided.");

        object[] keyValues = new object[rawValues.Length];
        for (int i = 0; i < rawValues.Length; i++)
        {
            Type clrType = keyProperties[i].ClrType;
            keyValues[i] = Convert.ChangeType(rawValues[i], clrType);
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
