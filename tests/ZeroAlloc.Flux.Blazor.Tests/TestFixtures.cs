using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using ZeroAlloc.Flux;
using ZeroAlloc.Flux.Blazor;

// MA0048 — TestFixtures.cs intentionally holds multiple feature/action/reducer/component
// types so each Blazor test can refer to a single coherent fixture set. Suppress file-wide.
#pragma warning disable MA0048

namespace ZeroAlloc.Flux.Blazor.Tests;

[Feature]
public readonly partial record struct TestCounterState(int Count);

public readonly record struct IncrementTestAction(int Amount);

public static partial class TestCounterReducers
{
    [Reducer]
    public static TestCounterState On(TestCounterState state, IncrementTestAction action)
        => state with { Count = state.Count + action.Amount };
}

// A minimal Blazor component derived from FluxComponent.
public sealed class TestCounter : FluxComponent
{
    [Inject] public IStore<TestCounterState> Store { get; set; } = default!;

    protected override void SubscribeStores() => Subscribe(Store);

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "p");
        builder.AddContent(1, "Count: ");
        builder.AddContent(2, Store.Value.Count);
        builder.CloseElement();
    }
}
