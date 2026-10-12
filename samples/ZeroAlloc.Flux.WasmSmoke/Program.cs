using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using ZeroAlloc.Flux.Generated;
using ZeroAlloc.Flux.WasmSmoke;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.Services.AddZeroAllocFlux();
await builder.Build().RunAsync().ConfigureAwait(false);