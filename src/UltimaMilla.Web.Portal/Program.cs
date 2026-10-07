using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using UltimaMilla.Web.Portal.Components;
using UltimaMilla.Web.Portal.Services;

var builder = WebApplication.CreateBuilder(args);

// Blazor InteractiveServer (informe 6.1). Con más de una réplica hará falta
// Data Protection y backplane de SignalR en Redis; con una sola no.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddHttpClient<EnviosApiClient>(c =>
    c.BaseAddress = new Uri(builder.Configuration["Api:BaseUrl"] ?? "http://localhost:5080/"));

// Telemetría (RNF 6.12): sale por OTLP al Collector, igual que la API. La instrumentación de
// HttpClient es la que propaga el traceparent, y por eso la traza se ve de punta a punta:
// Portal → API → PostgreSQL con un solo traceId.
builder.Logging.AddOpenTelemetry(o => o.IncludeFormattedMessage = true);
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation())
    .WithMetrics(m => m
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation())
    .UseOtlpExporter();

var app = builder.Build();

app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
