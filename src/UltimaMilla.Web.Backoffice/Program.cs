using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using UltimaMilla.Web.Backoffice.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddHttpClient<EnviosApiClient>(c =>
    c.BaseAddress = new Uri(builder.Configuration["Api:BaseUrl"] ?? "http://localhost:5080/"));
builder.Services.AddHttpClient<TarifasApiClient>(c =>
    c.BaseAddress = new Uri(builder.Configuration["Api:BaseUrl"] ?? "http://localhost:5080/"));

// Telemetría (RNF 6.12): sale por OTLP al Collector, igual que la API. La instrumentación de
// HttpClient es la que propaga el traceparent, y por eso la traza se ve de punta a punta:
// Backoffice → API → PostgreSQL con un solo traceId.
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

app.MapRazorPages();

app.Run();
