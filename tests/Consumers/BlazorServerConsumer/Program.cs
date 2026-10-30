using Essentieables;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.UseBlazorEssentieables();

var app = builder.Build();
var (pattern, html) = BlazorEssentials.UseWebAuthenticator();
app.MapGet(pattern, () => Results.Content(html, "text/html"));
app.UseStaticFiles();
app.Run();
