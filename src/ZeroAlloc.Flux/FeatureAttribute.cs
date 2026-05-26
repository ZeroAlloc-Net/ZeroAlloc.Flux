using System;

namespace ZeroAlloc.Flux;

/// <summary>
/// Marks a record struct / record class as a Flux feature state slice. The source generator
/// emits a <see cref="IStore{TState}"/> implementation registered as the corresponding service.
/// </summary>
/// <remarks>
/// <para>The target type must be <c>partial</c> so the generator can emit alongside it.</para>
/// <para>For features that need DI-bound or configuration-bound initialization, set
/// <see cref="InitialState"/> to the name of a <c>public static TState Method(IServiceProvider sp)</c>
/// factory method on the same type. Defaults to <see langword="null"/> — initial state is
/// produced via the parameterless constructor (<c>new TState()</c>).</para>
/// </remarks>
[AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class FeatureAttribute : Attribute
{
    /// <summary>
    /// Optional name of a <c>public static TState Method(IServiceProvider)</c> factory on the
    /// same type that produces the initial state. When <see langword="null"/>, the generator
    /// uses <c>new TState()</c>.
    /// </summary>
    public string? InitialState { get; set; }
}
