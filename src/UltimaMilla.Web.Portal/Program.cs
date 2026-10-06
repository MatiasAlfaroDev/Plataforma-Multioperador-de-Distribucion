using UltimaMilla.Web.Portal.Components;
using UltimaMilla.Web.Portal.Services;

var builder = WebApplication.CreateBuilder(args);

// Blazor InteractiveServer (informe 6.1). Con más de una réplica hará falta
// Data Protection y backplane de SignalR en Redis; con una sola no.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddHttpClient<EnviosApiClient>(c =>
    c.BaseAddress = new Uri(builder.Configuration["Api:BaseUrl"] ?? "http://localhost:5080/"));

var app = builder.Build();

app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
