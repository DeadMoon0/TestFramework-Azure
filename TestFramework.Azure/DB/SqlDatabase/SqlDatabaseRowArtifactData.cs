using Newtonsoft.Json;
using TestFramework.Core.Artifacts;

namespace TestFramework.Azure.DB.SqlDatabase;

/// <summary>
/// Artifact payload that stores a SQL row snapshot.
/// </summary>
/// <typeparam name="TRow">The row entity type.</typeparam>
public class SqlDatabaseRowArtifactData<TRow>(TRow row) : ArtifactData<SqlDatabaseRowArtifactData<TRow>, SqlDatabaseRowArtifactDescriber<TRow>, SqlDatabaseRowArtifactReference<TRow>>
    where TRow : class
{
    /// <summary>
    /// The captured row value.
    /// </summary>
    public TRow Row { get; } = row;

    /// <summary>
    /// Returns a readable string representation of the captured row.
    /// </summary>
    /// <returns>A string representation of the artifact data.</returns>
    public override string ToString()
    {
        try { return $"SQL Row<{typeof(TRow).Name}>: {JsonConvert.SerializeObject(Row)}"; }
        catch { return $"SQL Row<{typeof(TRow).Name}>"; }
    }
}
