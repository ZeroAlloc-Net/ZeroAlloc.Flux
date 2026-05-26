using System;

namespace ZeroAlloc.Flux;

/// <summary>
/// Read access to a Flux feature state slice. Source-generated per <see cref="FeatureAttribute"/>.
/// </summary>
/// <typeparam name="TState">The feature state type. Must be the same type decorated with
/// <see cref="FeatureAttribute"/> in the consuming compilation.</typeparam>
public interface IStore<TState>
{
    /// <summary>The current state value. Reads are lock-free.</summary>
    TState Value { get; }

    /// <summary>
    /// Fired after the state value has been atomically updated by a reducer. Subscribers run
    /// synchronously inside the dispatcher's update path; long-running subscribers should
    /// offload work via <see cref="System.Threading.Tasks.Task.Run(System.Action)"/>.
    /// </summary>
#pragma warning disable MA0046 // Use a derived type of EventArgs — intentional Flux design: single-arg Action<TState>.
    event Action<TState> StateChanged;
#pragma warning restore MA0046
}
