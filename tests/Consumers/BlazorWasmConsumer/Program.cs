using Essentieables;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.Services.UseBlazorEssentieables();

var host = builder.Build();
await BlazorEssentials.InitializeAsync(host.Services);
await host.RunAsync();
