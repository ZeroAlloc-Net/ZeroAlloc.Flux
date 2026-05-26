using System.Threading.Tasks;

namespace ZeroAlloc.Flux;

/// <summary>
/// Dispatches an action to all features whose reducers match the action type. The generator
/// emits a single implementation that fans out to each matching feature's <see cref="IStore{TState}"/>
/// in declaration order, awaiting each <see cref="IStore{TState}.StateChanged"/> handler.
/// </summary>
public interface IDispatcher
{
    /// <summary>
    /// Routes <paramref name="action"/> to every <see cref="FeatureAttribute"/>-decorated feature
    /// whose <see cref="ReducerAttribute"/>-decorated method signature matches
    /// <typeparamref name="TAction"/>.
    /// </summary>
    /// <typeparam name="TAction">The action type. Plain record struct / record class — no marker
    /// interface required. The generator discovers it from reducer signatures.</typeparam>
    /// <param name="action">The action instance. Passed to each matching reducer.</param>
    /// <returns>
    /// A <see cref="ValueTask"/> completing when every store's <see cref="IStore{TState}.StateChanged"/>
    /// handler chain has finished. Completes synchronously when no handler suspends — zero allocation
    /// on the hot path.
    /// </returns>
    ValueTask DispatchAsync<TAction>(TAction action);
}
