using System.Text.Json;
using TestFramework.Core.Artifacts;

namespace TestFramework.Azure.DB.SqlServer;

/// <summary>
/// Artifact payload that stores a SQL row snapshot.
/// </summary>
/// <typeparam name="TRow">The row entity type.</typeparam>
public class SqlRowArtifactData<TRow>(TRow row) : ArtifactData<SqlRowArtifactData<TRow>, SqlRowArtifactDescriber<TRow>, SqlRowArtifactReference<TRow>>
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
        try { return $"SQL Row<{typeof(TRow).Name}>: {JsonSerializer.Serialize(Row)}"; }
        catch { return $"SQL Row<{typeof(TRow).Name}>"; }
    }
}
