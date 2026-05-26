using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ZeroAlloc.Flux;
using ZeroAlloc.Flux.Generated;
using ZeroAlloc.TestHelpers;
using Xunit;

namespace ZeroAlloc.Flux.Tests;

// Allocation budgets for the documented hot paths of the generated Flux runtime. Each
// budgeted test wires the production DI container (services.AddZeroAllocFlux()) and drives
// the generated FluxDispatcher / Store_* directly through the IDispatcher / IStore<>
// interfaces — the way every consumer calls into them.
//
// AllocationGate only ships a Func<ValueTask<T>> overload; the dispatcher returns a plain
// ValueTask, so we drain it inline (the sync-completion check from AssertBudgetValueTask is
// duplicated here in-place). If the ValueTask suspends the test fails — which is exactly the
// budget contract.
public sealed class AllocationBudgetTests
{
    [Fact]
    public void DispatchAsync_SyncPath_ZeroAllocation()
    {
        // Single-feature dispatch with no subscribers. The generator emits a straight-line
        // return store.UpdateAsync(reducer.On(state, action)); the JIT collapses the
        // interface-level typeof-chain to a direct call for the known TAction. Budget: 0 B.
        var services = new ServiceCollection();
        services.AddZeroAllocFlux();
        using var sp = services.BuildServiceProvider();

        var dispatcher = sp.GetRequiredService<IDispatcher>();
        var action = new ResetAction();

        AllocationGate.AssertBudget(
            budgetBytes: 0,
            iterations: 1000,
            action: () =>
            {
                var t = dispatcher.DispatchAsync(action);
                if (!t.IsCompletedSuccessfully)
                {
                    throw new InvalidOperationException(
                        "AllocationGate: DispatchAsync did not complete synchronously — " +
                        "awaiter machinery would pollute the measurement.");
                }
            },
            label: "DispatchAsync (ResetAction, single feature, no subscribers)");
    }

    [Fact]
    public void StoreValue_Getter_ZeroAllocation()
    {
        // Library promise: IStore<T>.Value is a lock-free read. With T == record struct, the
        // returned value is copied to the caller's stack — zero heap allocation.
        var services = new ServiceCollection();
        services.AddZeroAllocFlux();
        using var sp = services.BuildServiceProvider();

        var counter = sp.GetRequiredService<IStore<CounterState>>();

        AllocationGate.AssertBudget(
            budgetBytes: 0,
            iterations: 1000,
            action: () => { _ = counter.Value; },
            label: "IStore<CounterState>.Value getter");
    }
}
