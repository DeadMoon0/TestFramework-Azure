using TestFramework.Core.Artifacts;

namespace TestFramework.Azure.DB.SqlServer;

/// <summary>
/// Static artifact kind for SQL row artifacts.
/// </summary>
/// <typeparam name="TRow">The row entity type.</typeparam>
public class AzureSqlRowArtifactKind<TRow> : ArtifactKind<AzureSqlRowArtifactDescriber<TRow>, AzureSqlRowArtifactData<TRow>, AzureSqlRowArtifactReference<TRow>>, IStaticArtifactKind<AzureSqlRowArtifactKind<TRow>>
    where TRow : class
{
    /// <summary>
    /// Singleton artifact kind instance.
    /// </summary>
    public static AzureSqlRowArtifactKind<TRow> Kind { get; } = new AzureSqlRowArtifactKind<TRow>();
}
