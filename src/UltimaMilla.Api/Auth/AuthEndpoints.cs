
using FluentValidation;
using UltimaMilla.Application.Auth.Commands.Login;
using UltimaMilla.Application.Auth.Commands.Register;

namespace UltimaMilla.Api.Auth;

public static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(
        this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/auth")
            .WithTags("Autenticación");

        grupo.MapPost("/login", LoginAsync)
            .AllowAnonymous();

        grupo.MapPost("/register", RegisterAsync)
            .AllowAnonymous();

        return grupo;
    }

    private static async Task<IResult> LoginAsync(
        LoginCommand command,
        LoginHandler handler,
        CancellationToken ct)
    {
        // 1. Validar los datos recibidos.
        // 2. Ejecutar el caso de uso de autenticación.
        LoginResult resultado;

        try
        {
            resultado = await handler.Handle(command, ct);
        }
        catch (ValidationException ex)
        {
            return Results.BadRequest(new
            {
                mensaje = "Los datos ingresados no son válidos.",
                errores = ex.Errors.Select(e => e.ErrorMessage)
            });
        }

        // 3. Convertir el resultado en una respuesta HTTP.
        return resultado.Status switch
        {
            LoginStatus.Exitoso when resultado.Response is not null
                => Results.Ok(resultado.Response),

            LoginStatus.CredencialesInvalidas
                => Results.Unauthorized(),

            LoginStatus.OperadorNoAutorizado
    => Results.Json(
        new
        {
            mensaje = "No se pudo determinar un contexto autorizado."
        },
        statusCode: StatusCodes.Status403Forbidden),

        LoginStatus.ComercioNoAutorizado
            => Results.Json(
                new
                {
                    mensaje = "No se pudo determinar un contexto autorizado."
                },
                statusCode: StatusCodes.Status403Forbidden),

        LoginStatus.TipoContextoInvalido
            => Results.BadRequest(new
            {
                mensaje = "El tipo de contexto debe ser OPERADOR o COMERCIO."
            }),

        _ => Results.Problem(
            "No se pudo procesar el inicio de sesión.")
                };
    }

    
private static async Task<IResult> RegisterAsync(
    RegisterCommand command,
    RegisterHandler handler,
    CancellationToken ct)
{
    RegisterResult resultado;

    try
    {
        resultado = await handler.Handle(command, ct);
    }
    catch (ValidationException ex)
    {
        return Results.BadRequest(new
        {
            mensaje = "Los datos ingresados no son válidos.",
            errores = ex.Errors.Select(e => e.ErrorMessage)
        });
    }

    return resultado.Status switch
    {
        RegisterStatus.Exitoso when resultado.Response is not null
            => Results.Created(
                $"/api/comercios/{resultado.Response.ComercioId}",
                resultado.Response),

        RegisterStatus.EmailYaRegistrado
            => Results.Conflict(new
            {
                mensaje = "El correo electrónico ya está registrado."
            }),

        RegisterStatus.RutYaRegistrado
            => Results.Conflict(new
            {
                mensaje = "Ya existe un comercio con ese RUT."
            }),

        RegisterStatus.DatosInvalidos
            => Results.BadRequest(new
            {
                mensaje = "No se pudo completar el registro.",
                errores = resultado.Errores
            }),

        _ => Results.Problem(
            "No se pudo procesar el registro.")
    };
}

}
