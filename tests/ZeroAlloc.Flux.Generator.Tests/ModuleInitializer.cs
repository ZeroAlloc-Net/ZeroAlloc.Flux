using System.Runtime.CompilerServices;
using VerifyTests;
using VerifyXunit;

namespace ZeroAlloc.Flux.Generator.Tests;

internal static class ModuleInitializer
{
    [ModuleInitializer]
    public static void Init()
    {
        VerifySourceGenerators.Initialize();

        // The test project multi-targets net8.0 + net10.0; the emitted source is identical
        // across frameworks, so we collapse the per-TFM snapshot files into one.
        VerifierSettings.DisableRequireUniquePrefix();
    }
}
