using Microsoft.CodeAnalysis;

namespace ZeroAlloc.Flux.Generator;

/// <summary>
/// Source generator for ZeroAlloc.Flux: discovers <c>[Feature]</c> types and their
/// <c>[Reducer]</c> methods, then emits per-feature <c>IStore&lt;TState&gt;</c> wiring and
/// DI registrations.
/// </summary>
/// <remarks>
/// <para>This class is a placeholder stub introduced in Phase 2 Batch A (Task 2.2) so that the
/// snapshot-test harness can reference the generator type. Task 2.9 replaces the
/// <see cref="Initialize"/> body with the real discovery + emit pipeline.</para>
/// </remarks>
[Generator(LanguageNames.CSharp)]
public sealed class FluxGenerator : IIncrementalGenerator
{
    /// <inheritdoc/>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Stub — see remarks. Task 2.9 wires the real pipeline.
    }
}
