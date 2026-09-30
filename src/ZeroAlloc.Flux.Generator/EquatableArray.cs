using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace ZeroAlloc.Flux.Generator;

/// <summary>
/// Element-wise equatable wrapper around <see cref="ImmutableArray{T}"/>. The default equality
/// of <see cref="ImmutableArray{T}"/> is reference equality of the underlying array, which
/// defeats incremental-generator caching when models are rebuilt per run.
/// </summary>
internal readonly record struct EquatableArray<T>(ImmutableArray<T> Values) : IEnumerable<T>
    where T : IEquatable<T>
{
    public static EquatableArray<T> Empty => new(ImmutableArray<T>.Empty);

    public int Count => Values.IsDefault ? 0 : Values.Length;

    public T this[int index] => Values[index];

    public bool Equals(EquatableArray<T> other)
    {
        // Default and empty compare equal, matching GetHashCode below.
        var left = Values.IsDefault ? ImmutableArray<T>.Empty : Values;
        var right = other.Values.IsDefault ? ImmutableArray<T>.Empty : other.Values;
        if (left.Length != right.Length) return false;

        var comparer = EqualityComparer<T>.Default;
        for (var i = 0; i < left.Length; i++)
        {
            if (!comparer.Equals(left[i], right[i])) return false;
        }

        return true;
    }

    public override int GetHashCode()
    {
        if (Values.IsDefault || Values.Length == 0) return 0;
        var hash = 17;
        foreach (var v in Values)
        {
            hash = unchecked((hash * 31) + (v?.GetHashCode() ?? 0));
        }
        return hash;
    }

    /// <summary>Struct enumerator, so <c>foreach</c> neither boxes nor allocates.</summary>
    public ImmutableArray<T>.Enumerator GetEnumerator() =>
        (Values.IsDefault ? ImmutableArray<T>.Empty : Values).GetEnumerator();

    IEnumerator<T> IEnumerable<T>.GetEnumerator() =>
        ((IEnumerable<T>)(Values.IsDefault ? ImmutableArray<T>.Empty : Values)).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<T>)this).GetEnumerator();
}
