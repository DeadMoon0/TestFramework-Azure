using TestFramework.Core.Artifacts;

namespace TestFramework.Azure.DB.SqlServer;

public class SqlRowArtifactKind<TRow> : ArtifactKind<SqlRowArtifactDescriber<TRow>, SqlRowArtifactData<TRow>, SqlRowArtifactReference<TRow>>, IStaticArtifactKind<SqlRowArtifactKind<TRow>>
    where TRow : class
{
    public static SqlRowArtifactKind<TRow> Kind { get; } = new SqlRowArtifactKind<TRow>();
}
