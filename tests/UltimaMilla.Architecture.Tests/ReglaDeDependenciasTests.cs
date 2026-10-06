using NetArchTest.Rules;
using UltimaMilla.Application.Envios.Commands.CrearEnvio;
using UltimaMilla.Domain.Envios;

namespace UltimaMilla.Architecture.Tests;

/// <summary>
/// Prueba exigida por la letra (6.1) y descripta en el informe (1.2):
/// si alguien rompe la regla de dependencias de Clean Architecture, el pipeline falla.
/// </summary>
public class ReglaDeDependenciasTests
{
    [Fact]
    public void Domain_no_depende_de_frameworks_ni_de_otras_capas()
    {
        var resultado = Types.InAssembly(typeof(Envio).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Microsoft.EntityFrameworkCore",
                "Microsoft.AspNetCore",
                "UltimaMilla.Application",
                "UltimaMilla.Infrastructure")
            .GetResult();

        Assert.True(resultado.IsSuccessful,
            "Tipos de Domain con dependencias prohibidas: " + string.Join(", ", resultado.FailingTypeNames ?? []));
    }

    [Fact]
    public void Application_no_depende_de_infraestructura_ni_de_la_web()
    {
        var resultado = Types.InAssembly(typeof(CrearEnvioHandler).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Microsoft.EntityFrameworkCore",
                "Microsoft.AspNetCore",
                "UltimaMilla.Infrastructure")
            .GetResult();

        Assert.True(resultado.IsSuccessful,
            "Tipos de Application con dependencias prohibidas: " + string.Join(", ", resultado.FailingTypeNames ?? []));
    }
}
