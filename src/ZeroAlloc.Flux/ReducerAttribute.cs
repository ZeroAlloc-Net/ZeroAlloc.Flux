using System;

namespace ZeroAlloc.Flux;

/// <summary>
/// Marks a <c>public static</c> method as a Flux reducer. The method takes exactly two
/// parameters: the first must be a type decorated with <see cref="FeatureAttribute"/>, and the
/// second is the action type. Neither may be <c>ref</c> or <c>out</c>. The return type must match
/// the first parameter type. Any other signature fires <c>ZFLUX003</c> at compile time.
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
