using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Core.Debugger;
using TestFramework.Core.Logging;
using TestFramework.Core.Timelines;
using TestFramework.Core.Variables;

namespace TestFramework.Azure.Tests;

/// <summary>
/// Builds a real <see cref="VariableStore"/> for tests that must not pass <c>null!</c>.
/// </summary>
/// <remarks>
/// Passing <c>null!</c> hides exactly the class of bug where a store is silently ignored, so tests
/// that exercise store-resolving code paths use this instead.
/// </remarks>
internal static class TestVariableStore
{
    internal static VariableStore Create()
    {
        Type debuggingRunSessionType = typeof(VariableStore).Assembly
            .GetType("TestFramework.Core.Debugger.DebuggingRunSession", throwOnError: true)!;

        object debuggingSession = debuggingRunSessionType
            .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Single()
            .Invoke([new SilentRunDebugger()]);

        ScopedLogger logger = (ScopedLogger)typeof(ScopedLogger)
            .GetMethod("CreateWithDebuggerSession", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, [debuggingSession])!;

        return (VariableStore)Activator.CreateInstance(
            typeof(VariableStore),
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            args: [logger, debuggingSession],
            culture: null)!;
    }

    private sealed class SilentRunDebugger : IRunDebugger
    {
        public Task SignalInitTimelineRunAsync(string sessionId, string name, string projectPath, TimelineRunStructure runStructure) => Task.CompletedTask;

        public Task SignalEntityTransitionAsync(string sessionId, DebugEntityKind entityKind, string? stage, int? stepId, DebugLifecycleState state, DebugLifecycleState? previousState = null, DebugLifecycleState? outcomeState = null) => Task.CompletedTask;

        public Task SignalValueUpdateAsync(string sessionId, string name, DebugValueKind valueKind, string? stage, int? stepId, DebugValueEnvelope value) => Task.CompletedTask;

        public Task SignalLogEntryAsync(string sessionId, DebugLogEntry entry) => Task.CompletedTask;

        public Task SignalAssertionAsync(string sessionId, DebugAssertionEntry entry) => Task.CompletedTask;

        public Task SignalTimelineRunFinishedAsync(string sessionId) => Task.CompletedTask;

        public Task SignalAndWaitBreakpointHitAsync(string sessionId, string stage, int stepId) => Task.CompletedTask;
    }
}
