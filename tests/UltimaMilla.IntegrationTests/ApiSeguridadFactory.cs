
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using NSubstitute;
using StackExchange.Redis;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using UltimaMilla.IntegrationTests.Seguridad;
using UltimaMilla.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace UltimaMilla.IntegrationTests;

public sealed class ApiSeguridadFactory : WebApplicationFactory<Program>
{
    public IOperadorRepository? RepositorioOperadores { get; set; }

    public IUnitOfWork? UnidadDeTrabajo { get; set; }

    protected override void ConfigureWebHost(
        IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // Estas variables deben existir antes de que Program.cs
        // lea las cadenas de conexión.
        Environment.SetEnvironmentVariable(
            "ConnectionStrings__Default",
            "Host=localhost;Database=ultimamilla_tests;Username=test;Password=test");

        Environment.SetEnvironmentVariable(
            "ConnectionStrings__RabbitMq",
            "amqp://guest:guest@localhost:5672");

        Environment.SetEnvironmentVariable(
            "SeguridadWeb__LimitarTasa",
            "false");

        Environment.SetEnvironmentVariable(
            "Inicializacion__SoloInicializar",
            "false");

        builder.ConfigureTestServices(services =>
{
    services.RemoveAll<
        UltimaMilla.Api.Salud.DrenajeAlApagar>();

    // Redis simulado: no requiere conexión real.
    var redis = Substitute.For<IConnectionMultiplexer>();

    redis.IsConnected.Returns(false);

    services.RemoveAll<IConnectionMultiplexer>();
    services.AddSingleton(redis);

    services
    .AddAuthentication()
    .AddScheme<
        AuthenticationSchemeOptions,
        TestAuthHandler>(
            TestAuthHandler.Esquema,
            _ => { });

services.PostConfigure<PolicySchemeOptions>(
    "CookieOClaveApi",
    options =>
    {
        options.ForwardDefaultSelector =
            _ => TestAuthHandler.Esquema;
    });

    if (RepositorioOperadores is not null)
{
    services.RemoveAll<IOperadorRepository>();
    services.AddScoped(_ => RepositorioOperadores!);
}

if (UnidadDeTrabajo is not null)
{
    services.RemoveAll<IUnitOfWork>();
    services.AddScoped(_ => UnidadDeTrabajo!);
}
});
    }
}
