using ZeroAlloc.Flux;
using Xunit;

namespace ZeroAlloc.Flux.Tests;

public sealed class FeatureAttributeTests
{
    [Fact]
    public void DefaultInitialStateIsNull()
    {
        var attr = new FeatureAttribute();
        Assert.Null(attr.InitialState);
    }

    [Fact]
    public void InitialStateIsSettable()
    {
        var attr = new FeatureAttribute { InitialState = "GetInitialState" };
        Assert.Equal("GetInitialState", attr.InitialState);
    }
}
