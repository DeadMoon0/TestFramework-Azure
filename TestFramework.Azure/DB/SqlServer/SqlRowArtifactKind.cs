using TestFramework.Core.Artifacts;

namespace TestFramework.Azure.DB.SqlServer;

/// <summary>
/// Static artifact kind for SQL row artifacts.
/// </summary>
/// <typeparam name="TRow">The row entity type.</typeparam>
public class SqlRowArtifactKind<TRow> : ArtifactKind<SqlRowArtifactDescriber<TRow>, SqlRowArtifactData<TRow>, SqlRowArtifactReference<TRow>>, IStaticArtifactKind<SqlRowArtifactKind<TRow>>
    where TRow : class
{
    /// <summary>
    /// Singleton artifact kind instance.
    /// </summary>
    public static SqlRowArtifactKind<TRow> Kind { get; } = new SqlRowArtifactKind<TRow>();
}
