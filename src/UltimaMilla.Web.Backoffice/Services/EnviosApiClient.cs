
using System.Net.Http.Json;

namespace UltimaMilla.Web.Backoffice.Services;

/// <summary>Datos que devuelve GET /api/operadores.</summary>
public sealed record OperadorItem(
    Guid Id,
    string Nombre,
    string ColorPrimario);

/// <summary>Datos que devuelve GET /api/envios.</summary>
public sealed record EnvioItem(
    Guid Id,
    string ReferenciaExterna,
    Guid OperadorId,
    string OperadorNombre,
    string ComercioNombre,
    string Modalidad,
    string Estado,
    string DestinatarioNombre,
    string Direccion,
    int CantidadBultos,
    decimal PesoTotalKg,
    decimal? TarifaCalculada,
    DateTimeOffset FechaAlta,
    int IntentosRealizados);

/// <summary>Detalle completo de un envío.</summary>
public sealed record DetalleEnvioItem(
    Guid Id,
    string ReferenciaExterna,
    Guid OperadorId,
    Guid CuentaComercialId,
    string Estado,
    string Modalidad,
    string Destinatario,
    string Direccion,
    DateTimeOffset FechaAlta,
    decimal? TarifaCalculada,
    int IntentosRealizados,
    int BultosEntregados,
    int BultosPendientes,
    int BultosEnDevolucion,
    int BultosDevueltos,
    bool PuedeIniciarDevolucion,
    IReadOnlyList<DetalleBultoItem> Bultos,
    IReadOnlyList<DetalleEventoItem> Eventos);

/// <summary>Información de un bulto del envío.</summary>
public sealed record DetalleBultoItem(
    Guid Id,
    string Identificacion,
    string Estado,
    decimal PesoKg,
    int AltoCm,
    int AnchoCm,
    int ProfundidadCm);

/// <summary>Evento registrado en el historial del envío.</summary>
public sealed record DetalleEventoItem(
    Guid Id,
    string EstadoAnterior,
    string EstadoNuevo,
    string Disparador,
    DateTimeOffset OcurridoEn,
    string Origen,
    string Responsable,
    string? Justificacion,
    string? ModalidadDevolucion,
    string? ReceptorDevolucion,
    string? ReferenciaConstanciaDevolucion);

/// <summary>Respuesta del endpoint antiforgery.</summary>
public sealed record TokenAntiforgeryItem(string Token);

public sealed record SolicitudCreadaItem(Guid SolicitudId);

public sealed class EnviosApiClient(HttpClient http)
{

    public async Task<IReadOnlyList<OperadorItem>> ListarOperadoresAsync(
        CancellationToken ct = default)
    {
        return await http.GetFromJsonAsync<List<OperadorItem>>(
            "api/operadores",
            ct) ?? [];
    }


    public async Task<IReadOnlyList<EnvioItem>> ListarEnviosAsync(
        CancellationToken ct = default)
    {
        return await http.GetFromJsonAsync<List<EnvioItem>>(
            "api/envios",
            ct) ?? [];
    }

    public async Task<DetalleEnvioItem?> ObtenerDetalleEnvioAsync(
        Guid envioId,
        CancellationToken ct = default)
    {
        return await http.GetFromJsonAsync<DetalleEnvioItem>(
            $"api/envios/{envioId}",
            ct);
    }

    public async Task IniciarDevolucionAsync(
        Guid envioId,
        string responsable,
        CancellationToken ct = default)
    {
        // 1. Obtener token antiforgery de la API.
        using var respuestaToken = await http.GetAsync(
            "api/antiforgery/token",
            ct);

        respuestaToken.EnsureSuccessStatusCode();

        var datosToken = await respuestaToken.Content
            .ReadFromJsonAsync<TokenAntiforgeryItem>(
                cancellationToken: ct);

        if (string.IsNullOrWhiteSpace(datosToken?.Token))
        {
            throw new InvalidOperationException(
                "No se pudo obtener el token de seguridad.");
        }

        // 2. Preparar la solicitud POST.
        using var solicitud = new HttpRequestMessage(
            HttpMethod.Post,
            $"api/envios/{envioId}/iniciar-devolucion");

        // 3. Agregar el token en el encabezado.
        solicitud.Headers.TryAddWithoutValidation(
            "X-CSRF-TOKEN",
            datosToken.Token);

        // 4. Recuperar la cookie antiforgery.
        if (respuestaToken.Headers.TryGetValues(
            "Set-Cookie",
            out var cookies))
        {
            var cookieAntiforgery = cookies
                .Select(c => c.Split(';')[0])
                .FirstOrDefault(c =>
                    c.StartsWith(
                        ".AspNetCore.Antiforgery.",
                        StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(cookieAntiforgery))
            {
                solicitud.Options.Set(
                    CookieAutenticacionHandler.CookieAntiforgeryKey,
                    cookieAntiforgery);
            }
        }

        // 5. Preparar el cuerpo de la solicitud.
        solicitud.Content = JsonContent.Create(new
        {
            Responsable = responsable
        });

        // 6. Enviar solicitud a la API.
        using var respuesta = await http.SendAsync(
            solicitud,
            ct);

        // 7. Controlar errores.
        if (!respuesta.IsSuccessStatusCode)
        {
            var detalle = await respuesta.Content
                .ReadAsStringAsync(ct);

            throw new HttpRequestException(
                string.IsNullOrWhiteSpace(detalle)
                    ? $"No se pudo iniciar la devolución (HTTP {(int)respuesta.StatusCode})."
                    : $"La API respondió: {detalle}",
                null,
                respuesta.StatusCode);
        }
    }

    public async Task RecibirEnDepositoAsync(
        Guid envioId,
        IReadOnlyList<string> identificaciones,
        string responsable,
        CancellationToken ct = default)
    {
        using var respuesta = await http.PostAsJsonAsync(
            $"api/envios/{envioId}/recepcion",
            new
            {
                IdentificacionesEscaneadas = identificaciones,
                Responsable = responsable
            },
            ct);

        if (!respuesta.IsSuccessStatusCode)
        {
            var detalle = await respuesta.Content
                .ReadAsStringAsync(ct);

            throw new HttpRequestException(
                string.IsNullOrWhiteSpace(detalle)
                    ? $"La API rechazó la recepción (HTTP {(int)respuesta.StatusCode})."
                    : $"La API respondió: {detalle}",
                null,
                respuesta.StatusCode);
        }
    }

    public async Task RetornarADepositoAsync(
        Guid envioId,
        IReadOnlyList<string> identificaciones,
        string responsable,
        CancellationToken ct = default)
    {
        using var respuesta = await http.PostAsJsonAsync(
            $"api/envios/{envioId}/retorno-deposito",
            new
            {
                IdentificacionesEscaneadas = identificaciones,
                Responsable = responsable
            },
            ct);

        respuesta.EnsureSuccessStatusCode();
    }


    // RF 41: el tablero de operación incorpora los retiros a domicilio.
    public async Task<IReadOnlyList<RetiroTableroItem>> ListarRetirosAsync(CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<RetiroTableroItem>>("api/retiros", ct) ?? [];

    /// <summary>accion: recibir-deposito, entregar-comercio o reprogramar.</summary>
    public async Task GestionarRetiroAsync(Guid retiroId, string accion, CancellationToken ct = default)
    {
        using var respuesta = await PostProtegidoAsync($"api/retiros/{retiroId}/{accion}", new { }, ct);

        if (!respuesta.IsSuccessStatusCode)
        {
            var detalle = await respuesta.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException(
                string.IsNullOrWhiteSpace(detalle)
                    ? $"La API rechazó la operación (HTTP {(int)respuesta.StatusCode})."
                    : $"La API respondió: {detalle}",
                null,
                respuesta.StatusCode);
        }
    }

        private async Task<HttpResponseMessage> PostProtegidoAsync(
            string ruta,
            object datos,
            CancellationToken ct)
        {
            using var respuestaToken = await http.GetAsync(
                "api/antiforgery/token", ct);

            respuestaToken.EnsureSuccessStatusCode();

            var datosToken = await respuestaToken.Content
                .ReadFromJsonAsync<TokenAntiforgeryItem>(
                    cancellationToken: ct);

            if (string.IsNullOrWhiteSpace(datosToken?.Token))
            {
                throw new InvalidOperationException(
                    "No se pudo obtener el token de seguridad.");
            }

            using var solicitud = new HttpRequestMessage(
                HttpMethod.Post, ruta);

            solicitud.Headers.TryAddWithoutValidation(
                "X-CSRF-TOKEN", datosToken.Token);

            if (respuestaToken.Headers.TryGetValues(
                "Set-Cookie", out var cookies))
            {
                var cookieAntiforgery = cookies
                    .Select(c => c.Split(';')[0])
                    .FirstOrDefault(c =>
                        c.StartsWith(
                            ".AspNetCore.Antiforgery.",
                            StringComparison.OrdinalIgnoreCase));

                if (!string.IsNullOrWhiteSpace(cookieAntiforgery))
                {
                    solicitud.Options.Set(
                        CookieAutenticacionHandler.CookieAntiforgeryKey,
                        cookieAntiforgery);
                }
            }

            solicitud.Content = JsonContent.Create(datos);

            return await http.SendAsync(solicitud, ct);
        }

    public async Task<Guid> SolicitarReprogramacionAsync(
        Guid envioId,
        CancellationToken ct = default)
    {
        using var respuesta = await PostProtegidoAsync(
            "api/solicitudes-reprogramacion",
            new { EnvioId = envioId },
            ct);

        await VerificarRespuestaReprogramacionAsync(
            respuesta, ct);

        var resultado = await respuesta.Content
            .ReadFromJsonAsync<SolicitudCreadaItem>(
                cancellationToken: ct);

        return resultado?.SolicitudId
            ?? throw new InvalidOperationException(
                "La API no devolvió el identificador de la solicitud.");
    }


    public async Task ConfirmarReprogramacionAsync(
        Guid solicitudId,
        string responsable,
        CancellationToken ct = default)
    {
        using var respuesta = await PostProtegidoAsync(
            $"api/solicitudes-reprogramacion/{solicitudId}/confirmar",
            new { Responsable = responsable },
            ct);

        await VerificarRespuestaReprogramacionAsync(
            respuesta, ct);
    }


    private static async Task VerificarRespuestaReprogramacionAsync(
        HttpResponseMessage respuesta,
        CancellationToken ct)
    {
        if (respuesta.IsSuccessStatusCode)
            return;

        var detalle = await respuesta.Content.ReadAsStringAsync(ct);

        throw new HttpRequestException(
            string.IsNullOrWhiteSpace(detalle)
                ? $"Error de reprogramación (HTTP {(int)respuesta.StatusCode})."
                : $"La API respondió: {detalle}",
            null,
            respuesta.StatusCode);
    }




}

// GET /api/retiros (lo que usa el tablero).
public sealed record RetiroDireccionItem(string Calle, string Numero, string Localidad, string Departamento);

public sealed record RetiroBultoItem(Guid Id, string Identificacion);

public sealed record RetiroTableroItem(
    Guid Id,
    string Motivo,
    DateOnly FechaSolicitada,
    string Estado,
    RetiroDireccionItem DomicilioRetiro,
    List<RetiroBultoItem> Bultos);
