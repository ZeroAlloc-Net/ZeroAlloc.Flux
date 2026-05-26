using System.Runtime.CompilerServices;
using VerifyXunit;

namespace ZeroAlloc.Flux.Generator.Tests;

internal static class ModuleInitializer
{
    [ModuleInitializer]
    public static void Init()
    {
        VerifySourceGenerators.Initialize();
    }
}
