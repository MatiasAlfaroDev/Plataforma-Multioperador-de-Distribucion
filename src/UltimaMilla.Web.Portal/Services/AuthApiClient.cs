
using System.Net.Http.Json;
using System.Text.Json;

namespace UltimaMilla.Web.Portal.Services;

public sealed class AuthApiClient(HttpClient http)
{
    public async Task<ResultadoRegistro> RegistrarAsync(
        RegistroModel modelo,
        CancellationToken ct = default)
    {
        try
        {
            var respuesta = await http.PostAsJsonAsync(
                "api/auth/register",
                modelo,
                ct);

            if (respuesta.IsSuccessStatusCode)
            {
                return new ResultadoRegistro(
                    true,
                    "Comercio registrado correctamente.");
            }

            var contenido = await respuesta.Content.ReadAsStringAsync(ct);

            try
            {
                using var documento = JsonDocument.Parse(contenido);
                var raiz = documento.RootElement;

                if (raiz.TryGetProperty("mensaje", out var mensaje))
                {
                    return new ResultadoRegistro(
                        false,
                        mensaje.GetString() ?? "No se pudo registrar el comercio.");
                }
            }
            catch (JsonException)
            {
                // Si la respuesta no es JSON, mostramos un error general.
            }

            return new ResultadoRegistro(
                false,
                $"No se pudo registrar el comercio. Código: {(int)respuesta.StatusCode}.");
        }
        catch (HttpRequestException)
        {
            return new ResultadoRegistro(
                false,
                "No se pudo conectar con la API.");
        }
    }
    public async Task<ResultadoLogin> LoginAsync(
    LoginModel modelo,
    CancellationToken ct = default)
{
    try
    {
        using var respuesta = await http.PostAsJsonAsync(
            "api/auth/login",
            new
            {
                modelo.Email,
                modelo.Password,
                OperadorId = (Guid?)null,
                TipoContexto = "COMERCIO",
                modelo.ComercioId
            },
            ct);

        if (respuesta.IsSuccessStatusCode)
        {
            var datos = await respuesta.Content
                .ReadFromJsonAsync<LoginResponseModel>(cancellationToken: ct);

            if (datos is not null && !string.IsNullOrWhiteSpace(datos.AccessToken))
                return new ResultadoLogin(true, "Inicio de sesión correcto.", datos);
        }

        return respuesta.StatusCode switch
        {
            System.Net.HttpStatusCode.Unauthorized =>
                new ResultadoLogin(false, "Correo o contraseña incorrectos.", null),

            System.Net.HttpStatusCode.Forbidden =>
                new ResultadoLogin(false, "No tenés autorización para ingresar a este comercio.", null),

            _ => new ResultadoLogin(false, "No se pudo iniciar sesión.", null)
        };
    }
    catch (HttpRequestException)
    {
        return new ResultadoLogin(false, "No se pudo conectar con la API.", null);
    }
    catch (System.Text.Json.JsonException)
    {
        return new ResultadoLogin(false, "La API devolvió una respuesta inesperada.", null);
    }
}
}



public sealed class RegistroModel
{
    public string NombreCompleto { get; set; } = string.Empty;
    public string NombreComercio { get; set; } = string.Empty;
    public string Rut { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    
}

public sealed record ResultadoRegistro(bool Exito, string Mensaje);

public sealed class LoginModel
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public Guid? ComercioId { get; set; }
}

public sealed record LoginResponseModel(
    string AccessToken,
    string TokenType,
    DateTime ExpiresAt,
    UsuarioLoginModel Usuario);

public sealed record UsuarioLoginModel(
    Guid Id,
    string Email,
    string NombreCompleto,
    Guid? OperadorId,
    string Rol,
    string TipoContexto,
    Guid? ComercioId);

public sealed record ResultadoLogin(
    bool Exito,
    string Mensaje,
    LoginResponseModel? Datos);