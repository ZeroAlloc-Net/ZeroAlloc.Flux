---
id: diagnostics
title: Diagnostics
sidebar_label: Diagnostics
sidebar_position: 6
---

# Diagnostics

The ZeroAlloc.Flux generator reports mistakes in features and reducers as compiler errors. Every ID
starts with `ZFLUX`, and every one is an error, so the build stops until it is fixed.

| ID | Title |
|---|---|
| [ZFLUX001](#zflux001) | A `[Reducer]` method's state parameter type isn't decorated with `[Feature]` |
| [ZFLUX002](#zflux002) | Two `[Reducer]` methods in the same class target the same state and action |
| [ZFLUX003](#zflux003) | A `[Reducer]` method has an invalid signature |
| [ZFLUX004](#zflux004) | The `[Feature(InitialState = ...)]` factory is missing or has the wrong signature |
| [ZFLUX005](#zflux005) | A `[Feature]` type is not declared `partial` |
| [ZFLUX006](#zflux006) | A `[Feature]` type is generic |
| [ZFLUX007](#zflux007) | A `[Feature]` type is not accessible to the rest of its assembly |
| [ZFLUX008](#zflux008) | Two `[Feature]` types have qualified names that differ only in case |
| [ZFLUX009](#zflux009) | A `[Feature]` type has no parameterless constructor and no `InitialState` factory |

The examples below each produce the error they describe.

## ZFLUX001

**A `[Reducer]` method's state parameter type isn't decorated with `[Feature]`.**

Every reducer updates a feature, so its first parameter must be a `[Feature]` type.

```csharp
public readonly record struct TodoState(int Count); // missing [Feature]
public readonly record struct AddTodo;

public static class TodoReducers
{
    [Reducer]
    public static TodoState On(TodoState state, AddTodo _) => new(state.Count + 1); // ZFLUX001
}
```

**Fix:** add `[Feature]` to the state type and make it `partial`.

## ZFLUX002

**Two `[Reducer]` methods in the same class target the same state and action.**

Within one class, each state and action pair may have one reducer. With two, it would be unclear
which result wins.

```csharp
[Feature]
public readonly partial record struct ScoreState(int Points);

public readonly record struct Scored(int Points);

public static class ScoreReducers
{
    [Reducer]
    public static ScoreState Add(ScoreState state, Scored action) => new(state.Points + action.Points);

    [Reducer]
    public static ScoreState Double(ScoreState state, Scored action) => new(state.Points * 2); // ZFLUX002
}
```

**Fix:** merge the two reducers into one. The error is reported at the later reducer, with the
earlier one as an additional location.

## ZFLUX003

**A `[Reducer]` method has an invalid signature.**

A reducer must be `public static`, take exactly two parameters, the state and the action, and
return the state type. The generated dispatcher calls it as `On(state, action)`, so a third
parameter, even an optional one, is rejected, and so is a `ref` or `out` parameter; `in` is fine.

```csharp
[Feature]
public readonly partial record struct ThemeState(bool Dark);

public readonly record struct ToggleTheme;

public static class ThemeReducers
{
    [Reducer]
    public static void On(ThemeState state, ToggleTheme _) { } // ZFLUX003: returns void
}
```

A reducer with a third parameter is rejected the same way:

```csharp
public static class ThemeReducers
{
    [Reducer]
    public static ThemeState On(ThemeState state, ToggleTheme _, bool force) => new(force || !state.Dark); // ZFLUX003
}
```

**Fix:** make the method `public static`, give it exactly the state and the action, and return the
new state: `public static ThemeState On(ThemeState state, ToggleTheme _) => new(!state.Dark);`.
A reducer that needs more input gets it from the action.

## ZFLUX004

**The `[Feature(InitialState = ...)]` factory is missing or has the wrong signature.**

The factory named in `InitialState` must be declared on the feature type as
`public static TFeature Name(IServiceProvider)`.

```csharp
[Feature(InitialState = nameof(Create))]
public readonly partial record struct LimitState(int Max)
{
    public static LimitState Create() => new(100); // ZFLUX004: no IServiceProvider parameter
}
```

**Fix:** give the factory the expected signature:
`public static LimitState Create(IServiceProvider services) => new(100);`. A factory that does not
exist is reported at the `InitialState` argument, and one with the wrong signature at the method.
Until it is fixed the feature gets no store, so ZFLUX004 is the only error.

## ZFLUX005

**A `[Feature]` type is not declared `partial`.**

The generator emits code alongside each feature, so the type must be `partial`.

```csharp
[Feature]
public readonly record struct FilterState(string Text); // ZFLUX005
```

**Fix:** add `partial`: `public readonly partial record struct FilterState(string Text);`

## ZFLUX006

**A `[Feature]` type is generic.**

A store holds one closed state type, and the generator never sees which type arguments a generic
feature would be used with. A feature that is generic, or nested in a generic type, gets no store.

```csharp
[Feature]
public readonly partial record struct ListState<T>(int Count); // ZFLUX006
```

**Fix:** declare one non-generic feature per state you need, such as
`ProductListState` and `OrderListState`.

## ZFLUX007

**A `[Feature]` type is not accessible to the rest of its assembly.**

The generated store, dispatcher and registration live in the `ZeroAlloc.Flux.Generated` namespace,
so they must be able to name the feature. A `private`, `protected` or `file` feature, or one nested
in such a type, gets no store.

```csharp
public partial class Shell
{
    [Feature]
    private readonly partial record struct MenuState(bool Open); // ZFLUX007
}
```

**Fix:** make the feature, and every type that contains it, `public` or `internal`.

## ZFLUX008

**Two `[Feature]` types have qualified names that differ only in case.**

Each store goes in a file named after its feature, and Roslyn compares file names ignoring case,
so two such features cannot both have a store. The one declared first keeps its store; the other
reports ZFLUX008 and gets none.

```csharp
namespace App
{
    [Feature]
    public readonly partial record struct UserState(int Id);

    [Feature]
    public readonly partial record struct Userstate(int Id); // ZFLUX008
}
```

**Fix:** rename one of the two types.

## ZFLUX009

**A `[Feature]` type has no parameterless constructor and no `InitialState` factory.**

Without an `InitialState` factory, the generated store creates the initial state with
`new TState()`. A record struct always allows that. A record class allows it only with a `public`
or `internal` parameterless constructor, which a record class with only a primary constructor does
not have. An `abstract` class cannot be created at all. Such a feature gets no store.

```csharp
[Feature]
public sealed partial record SettingsState(string Theme); // ZFLUX009
```

**Fix:** add a parameterless constructor:
`public SettingsState() : this("light") { }`, or name a factory with
`[Feature(InitialState = nameof(Create))]`, see [ZFLUX004](#zflux004), or make the feature a
`record struct`.
