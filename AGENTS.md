# AGENTS.md: ZeroAlloc.Flux

Source-generated Flux/Redux state management for .NET and Blazor. Users write `[Feature]` state records and `[Reducer]` static methods; a Roslyn generator emits the stores, the dispatcher and `AddZeroAllocFlux`. It ships as two NuGet packages, `ZeroAlloc.Flux` (with the generator bundled inside) and `ZeroAlloc.Flux.Blazor`.

## Layout

- `src/ZeroAlloc.Flux.Generator` targets `netstandard2.0`, is not packable, and is bundled into the `ZeroAlloc.Flux` package under `analyzers/dotnet/cs` by the `IncludeGeneratorInPackage` target. `ZeroAlloc.Flux` references it with `ReferenceOutputAssembly="false"` on purpose: the generator must not run over the library itself.
- `src/ZeroAlloc.Flux` and `src/ZeroAlloc.Flux.Blazor` multi-target `net8.0;net10.0;net11.0`. Both track their public API in `PublicAPI.Shipped.txt` and `PublicAPI.Unshipped.txt`; RS0016 and RS0017 are errors, so a new or removed public member needs an Unshipped entry.
- The generator reports ZFLUX001 to ZFLUX009, tracked in `src/ZeroAlloc.Flux.Generator/AnalyzerReleases.Unshipped.md` and documented in `docs/diagnostics.md`. A new or changed diagnostic updates all three.
- `Directory.Build.targets` promotes `ZeroAlloc.Pipeline.Generators` and `ZeroAlloc.Pipeline` to analyzer dependencies; without it Roslyn cannot load the generator's dependencies.
- `samples/` holds `Sample`, `AotSmoke` (NativeAOT publish) and `WasmSmoke` (Blazor WebAssembly). They are not packed. `samples/Directory.Build.props` disables MA0048 there on purpose.
- `tests/ZeroAlloc.Flux.Generator.Tests/Snapshots` holds `*.verified.txt` files compared against generator output. Changing emitted code means updating the matching snapshot.
- `scripts/verify-generator-pack-layout.sh` looks for `ZeroAlloc.Mediator.Generator.*.nupkg`; it is a leftover from another repo and no workflow runs it.

## Build and test

`global.json` pins SDK `11.0.100` with `rollForward: latestMinor`. The test projects target `net8.0`, `net10.0` and `net11.0`, so a machine needs the .NET 8, 10 and 11 runtimes (ASP.NET Core 8, 10 and 11 for the Blazor tests), not only the SDK.

CI job `build` in `ci.yml` (`VERSION` is the GitVersion SemVer; `dotnet tool restore` then `dotnet gitversion /showvariable SemVer`):

```bash
dotnet restore
dotnet build --no-restore -c Release -p:Version=$VERSION
dotnet test --no-build -c Release --verbosity normal --collect "XPlat Code Coverage" --settings coverage.runsettings --results-directory TestResults
dotnet build samples/ZeroAlloc.Flux.WasmSmoke --no-restore -c Release -p:Version=$VERSION
dotnet pack src/ZeroAlloc.Flux/ZeroAlloc.Flux.csproj --no-build -c Release -p:PackageVersion=$VERSION -o ./artifacts
dotnet pack src/ZeroAlloc.Flux.Blazor/ZeroAlloc.Flux.Blazor.csproj --no-build -c Release -p:PackageVersion=$VERSION -o ./artifacts
```

The same job then gates on 80 percent line coverage (`coverage-gate` from ci-baseline). CI job `aot-smoke` in `ci.yml`:

```bash
dotnet restore samples/ZeroAlloc.Flux.AotSmoke/ZeroAlloc.Flux.AotSmoke.csproj
dotnet publish samples/ZeroAlloc.Flux.AotSmoke/ZeroAlloc.Flux.AotSmoke.csproj -r linux-x64 -c Release -o ./aot-out
timeout --kill-after=10s 120s ./aot-out/ZeroAlloc.Flux.AotSmoke
```

NativeAOT needs clang and `zlib1g-dev`. `benchmarks.yml` job `smoke` builds and runs `benchmarks/ZeroAlloc.Flux.Benchmarks` through the shared org workflow.

Fast inner loop, run in `REPO_DIR` and passing:

```bash
dotnet build ZeroAlloc.Flux.slnx -c Release
dotnet test tests/ZeroAlloc.Flux.Generator.Tests --no-build -c Release -f net8.0
dotnet test tests/ZeroAlloc.Flux.Tests --no-build -c Release -f net8.0 --filter "FullyQualifiedName~AllocationBudget"
```

## Conventions and gotchas

- `TreatWarningsAsErrors` and `EnforceCodeStyleInBuild` are on, with Meziantou, Roslynator and ZeroAlloc analyzers on every non-`netstandard2.0` project. Fix a warning at its source; do not suppress it.
- `dotnet pack` fails with ZAVERSION001 or ZAVERSION002 unless the version comes from `-p:Version` or `-p:PackageVersion`, and the assemblies being packed must carry that same version. When packing with `--no-build`, pass `-p:Version` to the build step too. A bare local `dotnet pack` is expected to fail.
- Allocation budgets are tests, not comments: `tests/ZeroAlloc.Flux.Tests/AllocationBudgetTests.cs` and the Blazor `FluxComponentTests` assert 0 B per call over 1,000 iterations with `AllocationGate`. A change to the dispatch, store or `StateChanged` path must keep them at 0 B on all three target frameworks. The README performance table states the same budgets.
- Generated code must stay straight-line: a direct call per action type, a non-capturing lambda, no handler dictionary and no reflection. Do not add a runtime lookup to the emitted dispatcher.
- `ZeroAlloc.Flux.Blazor` references `Microsoft.AspNetCore.Components` per target framework, not the `Microsoft.AspNetCore.App` framework, so it keeps working from Blazor WebAssembly. Do not switch it to a `FrameworkReference`.
- The generator project is `netstandard2.0` with `EnforceExtendedAnalyzerRules`: no APIs newer than netstandard2.0, and the incremental pipeline types (`EquatableArray`, `LocationInfo`) exist so models compare by value. `IncrementalityTests` guards this.

<!-- org-core:start sha256=8faf417fa86f11bd8ad9bc98dfa4683fba44e440291e84c6cb9b0ec3ca0c0e7e (generated from docker-nuc scripts/agents-md/org-core-zeroalloc.md; do not edit here) -->
## Org rules

These rules hold in every ZeroAlloc-Net repo. Each library makes two promises: it works under
NativeAOT and trimming, and its hot path allocates nothing. The 2026-10-11 sweep found 16 of 28 repos
allocating on the hot path, most with docs claiming 0 B.

### Hot path

The operation a consumer runs repeatedly: `Send`, a cache hit, `Validate`, `Map`, `Serialize`, a
resilience call, a state transition, an append, a worker poll, including the idle poll. Startup and
registration are not hot path. For a source generator, the hot path is the code it emits: judge the
snapshots and the generated output.

### Allocation traps the sweep found

| Pattern | Instead |
|---|---|
| `async ValueTask` that only awaits and returns | `return new ValueTask<T>(inner.XAsync(ct));` when the method only forwards a `Task<T>` (return a forwarded `ValueTask<T>` directly). The wrapper stores a synchronous throw in the returned task; the direct return lets it, and the cancellation status of the callee, reach the caller. Keep the `async` wrapper when callers depend on either |
| `Task.WhenAll` over tasks that already completed | skip the await only when every task has `IsCompletedSuccessfully` (`Status == TaskStatus.RanToCompletion` on `netstandard2.0`); otherwise `await Task.WhenAll`, which also reports faults of completed tasks |
| `TagList` or log `params object[]` built before `Enabled`/`IsEnabled` | check first, or `LoggerMessage.Define` |
| event args allocated before checking for subscribers | copy the handler to a local, check, then allocate |
| `new Serializer()` / `new Utf8JsonWriter()` per call | cache stateless instances, reuse writers with `Reset` |
| `GetString()` / `ToString()` round-trips in JSON converters | parse and format through spans |
| `ToArray()` / `ToList()` copies between layers | pass `ReadOnlyMemory<T>` or spans, copy once at most |
| LINQ in emitted code | emit `for` loops |
| `yield` iterator behind a public `GetEnumerator` | public struct enumerator, from the first release |
| `Enum.IsDefined(typeof(E), v)` with a boxed value, `decimal.GetBits(v)` to read the scale | `Enum.IsDefined<E>(v)` when `v` has type `E` (.NET 5 or later, so not on .NET Framework, `netstandard2.0`, `netstandard2.1` or .NET Core 3.1); `v.Scale` for a scale read only (.NET 7 or later, never for the coefficient or sign). Keep the old call on older targets |
| configuration parsed or read per call | parse once and cache |
| an `AsyncLocal` write per call | pass state explicitly |
| a capturing lambda or method-group delegate per call | cache the delegate in a field, or a static lambda |
| a per-key object that is never removed | remove on release; otherwise it is a leak |

Inherent allocations are fine when documented: the consumer's own result, an exception on a failure
path, a one-time cache fill.

### Tests and benchmarks

- Every hot path has an allocation gate test (`AllocationGate` from ZeroAlloc.TestHelpers) that measures
  the whole public call, including any state setup.
- Budgets are exact. If a path allocates, assert that number and comment what it is.
- Test the generated code, not a hand-written stand-in, and cover every shape: each attribute, nested
  collections, and the asynchronous case with a fake that returns pending tasks.
- Benchmarks run in CI through the shared `benchmark-smoke.yml` and target the library's LTS
  frameworks. The benchmark body allocates nothing besides the library itself. Run locally with
  `--inProcess`.
- Publish exact allocations and same-run ratios against a `Baseline`, never absolute timings. Run with
  `--launchCount 3`, pin CPUs with `taskset`, give the spread, and label every table.

### NativeAOT and trimming

- Packable runtime projects, and projects consumers publish, set `IsAotCompatible=true` on modern
  target frameworks. Analyzers and source generators target `netstandard2.0`.
- No reflection or runtime code generation on a supported path. An opt-in reflection fallback carries
  honest `[RequiresUnreferencedCode]` / `[RequiresDynamicCode]`. Every suppression must be true;
  remove dead ones.
- The AOT smoke sample publishes with `TreatWarningsAsErrors=true`. If a third-party warning really
  blocks it, use a narrow, justified suppression for that warning. `WarningsNotAsErrors` selects codes,
  not assemblies, so it would also downgrade the same warning from generated code.
- The smoke runs the hot path of every package in the repo. A package without a smoke has unknown AOT
  status. Adapters run against real services in CI service containers; publish-only is a last resort.
  App and template smokes make authenticated requests.

### Docs and shipping

- Docs match measurements. After a hot-path change, re-measure and update `docs/performance.md` and the
  README. Never write "0 B" without a test that asserts it.
- New public API is `feat:`, even for a performance change. Public API changes are additive: never add
  an optional parameter to a shipped signature. A breaking end state goes in a `next-major` issue.
- A behaviour change gets a `## Behaviour change` section in the PR body.
- Conventional Commits with release-please. Subjects start lowercase, body lines stay within 100
  characters, and a body never contains nested parentheses: release-please drops the whole commit.
- Local packs use `9.9.9-local.<sha>`. After a release, verify the package on NuGet.
- Fix review findings in the PR; file an issue only for what cannot go in it.

Full rules, template wiring lessons and the reasons behind each rule: https://github.com/ZeroAlloc-Net/.github/blob/main/AGENTS.md
<!-- org-core:end -->
