using Xunit;

namespace ZeroAlloc.Flux.Generator.Tests;

public sealed class TestHarnessSmokeTests
{
    [Fact]
    public void TestHarness_CompilesEmptySource()
    {
        // Sanity check: the harness can compile an empty C# source without throwing.
        // Verifies the references + compilation setup are wired and that FluxGenerator
        // can be instantiated and driven. Empty source must produce no diagnostics.
        var diagnostics = TestHarness.RunDiagnostics(string.Empty);
        Assert.Empty(diagnostics);
    }
}
