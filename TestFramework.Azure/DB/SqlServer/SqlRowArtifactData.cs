using System.Text.Json;
using TestFramework.Core.Artifacts;

namespace TestFramework.Azure.DB.SqlServer;

public class SqlRowArtifactData<TRow>(TRow row) : ArtifactData<SqlRowArtifactData<TRow>, SqlRowArtifactDescriber<TRow>, SqlRowArtifactReference<TRow>>
    where TRow : class
{
    public TRow Row { get; } = row;

    public override string ToString()
    {
        try { return $"SQL Row<{typeof(TRow).Name}>: {JsonSerializer.Serialize(Row)}"; }
        catch { return $"SQL Row<{typeof(TRow).Name}>"; }
    }
}
