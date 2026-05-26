using ZeroAlloc.Flux;
using Xunit;

namespace ZeroAlloc.Flux.Tests;

public sealed class ReducerAttributeTests
{
    [Fact]
    public void AttributeTargetsMethod()
    {
        var usage = (System.AttributeUsageAttribute)typeof(ReducerAttribute)
            .GetCustomAttributes(typeof(System.AttributeUsageAttribute), false)[0];
        Assert.Equal(System.AttributeTargets.Method, usage.ValidOn);
        Assert.False(usage.AllowMultiple);
    }
}
