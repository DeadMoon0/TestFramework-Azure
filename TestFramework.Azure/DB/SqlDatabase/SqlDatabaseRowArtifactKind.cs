using TestFramework.Core.Artifacts;

namespace TestFramework.Azure.DB.SqlDatabase;

/// <summary>
/// Static artifact kind for SQL row artifacts.
/// </summary>
/// <typeparam name="TRow">The row entity type.</typeparam>
public class SqlDatabaseRowArtifactKind<TRow> : ArtifactKind<SqlDatabaseRowArtifactDescriber<TRow>, SqlDatabaseRowArtifactData<TRow>, SqlDatabaseRowArtifactReference<TRow>>, IStaticArtifactKind<SqlDatabaseRowArtifactKind<TRow>>
    where TRow : class
{
    /// <summary>
    /// Singleton artifact kind instance.
    /// </summary>
    public static SqlDatabaseRowArtifactKind<TRow> Kind { get; } = new SqlDatabaseRowArtifactKind<TRow>();
}
