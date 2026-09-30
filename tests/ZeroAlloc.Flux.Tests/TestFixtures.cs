using System.Runtime.InteropServices;
using ZeroAlloc.Flux;

// MA0048 — TestFixtures.cs intentionally holds multiple feature/action/reducer types
// so each runtime test can refer to a single coherent fixture set. Suppress file-wide.
#pragma warning disable MA0048

namespace ZeroAlloc.Flux.Tests;

[Feature]
public readonly partial record struct CounterState(int Count);

public readonly record struct IncrementAction(int Amount);
public readonly record struct ResetAction;

public static partial class CounterReducers
{
    [Reducer]
    public static CounterState On(CounterState state, IncrementAction action)
        => state with { Count = state.Count + action.Amount };

    [Reducer]
    public static CounterState On(CounterState state, ResetAction _) => new(0);
}

[Feature]
public readonly partial record struct BadgeCountState(int Count);

public static partial class BadgeReducers
{
    [Reducer]
    public static BadgeCountState On(BadgeCountState state, IncrementAction action)
        => state with { Count = state.Count + action.Amount };
}

[Feature]
public sealed partial record SettingsState(string Theme)
{
    public SettingsState() : this("default") { }
}

public readonly record struct UpdateThemeAction(string NewTheme);

public static partial class SettingsReducers
{
    [Reducer]
    public static SettingsState On(SettingsState state, UpdateThemeAction action)
        => state with { Theme = action.NewTheme };
}

// Record-class feature with a counting reducer, for the concurrent-dispatch tests of the CAS path.
[Feature]
public sealed partial record TallyState(int Count)
{
    public TallyState() : this(0) { }
}

public readonly record struct TallyAction;

public static partial class TallyReducers
{
    [Reducer]
    public static TallyState On(TallyState state, TallyAction _) => new(state.Count + 1);
}

// Struct feature wider than a pointer whose fields always agree, for the torn-read test. The
// runtime copies a struct this size field by field, so an unguarded read can mix two states.
[Feature]
[StructLayout(LayoutKind.Sequential)]
public readonly partial record struct WideState(long A, long B, long C, long D);

public readonly record struct WideStepAction;

public static partial class WideReducers
{
    [Reducer]
    public static WideState On(WideState state, WideStepAction _)
        => new(state.A + 1, state.B + 1, state.C + 1, state.D + 1);
}
