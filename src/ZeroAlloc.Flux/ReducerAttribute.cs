using System;

namespace ZeroAlloc.Flux;

/// <summary>
/// Marks a <c>public static</c> method as a Flux reducer. The method's first parameter
/// must be a type decorated with <see cref="FeatureAttribute"/>; the second parameter is the
/// action type. The return type must match the first parameter type.
/// </summary>
/// <remarks>
/// <para>Within a single feature, two reducers targeting the same action type fire
/// <c>ZFLUX002</c> at compile time. Across features, multiple reducers for the same action
/// type are allowed — they fan out at dispatch time (one update per feature).</para>
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class ReducerAttribute : Attribute
{
}
