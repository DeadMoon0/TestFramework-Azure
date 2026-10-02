using TestFramework.Core.Steps;
using TestFramework.Core.Variables;

namespace TestFramework.Azure.Tests;

/// <summary>
/// Builds a real <see cref="VariableStore"/> for tests that must not pass <c>null!</c>.
/// </summary>
/// <remarks>
/// Passing <c>null!</c> hides exactly the class of bug where a store is silently ignored, so tests
/// that exercise store-resolving code paths use this instead. A detached context's store is the public way
/// to get one; reflecting over the store's internal constructor broke the moment that constructor changed.
/// </remarks>
internal static class TestVariableStore
{
    internal static VariableStore Create() => RunContext.Detached().Variables;
}
