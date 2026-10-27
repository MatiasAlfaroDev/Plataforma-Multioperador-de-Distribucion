
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UltimaMilla.Application.Common;

namespace UltimaMilla.Api.Errores;

/// <summary>
/// Traduce las excepciones de Application y Domain a respuestas HTTP
/// con formato ProblemDetails.
/// </summary>
public sealed class ManejadorDeErrores(
    ILogger<ManejadorDeErrores> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext http,
        Exception exception,
        CancellationToken ct)
    {
        if (exception is UnauthorizedAccessException)
        {
            logger.LogWarning(
                "Acceso denegado en {Metodo} {Ruta}: {Motivo}",
                http.Request.Method,
                http.Request.Path,
                exception.Message);
        }
        else
        {
            logger.LogError(
                exception,
                "Error procesando {Metodo} {Ruta}",
                http.Request.Method,
                http.Request.Path);
        }

        if (exception is DbUpdateConcurrencyException concurrencia)
        {
            foreach (var entrada in concurrencia.Entries)
            {
                logger.LogError(
                    "Conflicto de concurrencia en entidad {Entidad}, estado EF: {Estado}",
                    entrada.Metadata.ClrType.Name,
                    entrada.State);
            }
        }

        ProblemDetails problema = exception switch
        {
            ValidationException ve => new ValidationProblemDetails(
                ve.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Select(e => e.ErrorMessage).ToArray()))
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Los datos enviados no son válidos."
            },

            UnauthorizedAccessException => new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "No tiene permisos para realizar esta operación."
            },

            NoEncontradoException => new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = exception.Message
            },

            ConflictoException => new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = exception.Message
            },

            ArgumentException or InvalidOperationException => new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = exception.Message
            },

            _ => new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Error inesperado."
            }
        };

        http.Response.StatusCode =
            problema.Status ?? StatusCodes.Status500InternalServerError;

        await http.Response.WriteAsJsonAsync(
            problema,
            problema.GetType(),
            ct);

        return true;
    }
}
