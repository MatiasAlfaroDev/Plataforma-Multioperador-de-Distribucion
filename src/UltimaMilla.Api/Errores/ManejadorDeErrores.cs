using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using UltimaMilla.Application.Common;

namespace UltimaMilla.Api.Errores;

/// <summary>
/// Traduce las excepciones de Application y Domain a respuestas HTTP con formato ProblemDetails,
/// para que los endpoints no tengan try/catch.
/// </summary>
public sealed class ManejadorDeErrores : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception exception, CancellationToken ct)
    {
        ProblemDetails problema = exception switch
        {
            ValidationException ve => new ValidationProblemDetails(
                ve.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()))
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Los datos enviados no son válidos."
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
            // Reglas del dominio violadas (por ejemplo, cuenta inactiva o bulto sin peso).
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

        http.Response.StatusCode = problema.Status ?? StatusCodes.Status500InternalServerError;
        // Se serializa con el tipo real para no perder la lista de errores de ValidationProblemDetails.
        await http.Response.WriteAsJsonAsync(problema, problema.GetType(), ct);
        return true;
    }
}
