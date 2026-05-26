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
