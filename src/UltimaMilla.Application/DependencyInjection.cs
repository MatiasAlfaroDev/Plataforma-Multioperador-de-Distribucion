using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using UltimaMilla.Application.CuentasComerciales.Queries.ListarCuentasComerciales;
using UltimaMilla.Application.Envios.Commands.CrearEnvio;
using UltimaMilla.Application.Envios.Queries.ListarEnvios;
using UltimaMilla.Application.Operadores.Queries.ListarOperadores;
using UltimaMilla.Application.Configuracion.Commands.ActualizarTarifa;
using UltimaMilla.Application.Configuracion.Queries.ObtenerTarifaVigente;
using UltimaMilla.Application.Configuracion.Queries.ListarZonas;

namespace UltimaMilla.Application;

public static class DependencyInjection
{
    /// <summary>Registra Handlers y Validators de Application.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<CrearEnvioValidator>();

        services.AddScoped<CrearEnvioHandler>();
        services.AddScoped<ListarEnviosHandler>();
        services.AddScoped<ListarCuentasComercialesHandler>();
        services.AddScoped<ListarOperadoresHandler>();
        services.AddScoped<ActualizarTarifaHandler>();
        services.AddScoped<ObtenerTarifaVigenteHandler>();
        services.AddScoped<ListarZonasHandler>();
        return services;
    }
}
