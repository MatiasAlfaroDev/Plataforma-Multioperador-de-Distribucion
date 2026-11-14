using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Common;
using UltimaMilla.Application.Envios.Commands.RetornarADeposito;
using UltimaMilla.Application.Retiros.Commands.GestionarRetiro;
using UltimaMilla.Application.Seguridad;
using UltimaMilla.Domain.Envios;
using UltimaMilla.Domain.Retiros;
using UltimaMilla.Domain.Rutas;

namespace UltimaMilla.Application.Rutas.Commands.RendirHojaDeRuta;

/// <summary>RF 22, segundo paso (Backoffice): el depósito controla lo que rindió el repartidor y lo recibe.</summary>
/// <param name="BultosEscaneados">Lo que el depósito recibe, escaneado al controlar.</param>
/// <param name="Responsable">Quién recibe en el depósito (queda en el historial de cada envío).</param>
public sealed record RendirHojaDeRutaCommand(
    Guid HojaDeRutaId,
    IReadOnlyList<string> BultosEscaneados,
    string Responsable);

/// <summary>Algo que tiene que volver al depósito: los bultos pendientes de un envío o los de un retiro hecho.</summary>
public sealed record RetornoEsperadoDto(string Tipo, Guid Id, string Referencia, IReadOnlyList<string> Bultos);

/// <summary>Lo que el depósito necesita ver para rendir la hoja.</summary>
public sealed record RendicionDto(
    Guid HojaDeRutaId,
    string Estado,
    IReadOnlyList<RetornoEsperadoDto> Retornos,
    IReadOnlyList<string> ParadasSinResultado);

public sealed record ResultadoRendicion(int EnviosRetornados, int RetirosRecibidos);

/// <summary>RF 22, primer paso (app): el repartidor rinde lo que trae de vuelta, escaneado al llegar al depósito.</summary>
public sealed record DeclararRendicionCommand(Guid HojaDeRutaId, IReadOnlyList<string> BultosEscaneados);

public sealed record ResultadoDeclaracion(int BultosDevueltos);

/// <summary>
/// RF 22: rendición al regresar al depósito, en dos pasos.
/// 1. El repartidor rinde desde la app: escanea lo que trae de vuelta (bultos no entregados, pendientes de una
///    entrega parcial y bultos retirados en domicilios); si coincide con lo que tiene que volver, la hoja queda
///    "Pendiente de control". No cambia ningún envío todavía.
/// 2. El depósito controla en el Backoffice: vuelve a escanear lo que recibe y, si coincide, cada envío vuelve
///    "En depósito" (con las reglas de nueva visita o devolución), cada retiro queda "En depósito" y la hoja se cierra.
/// Reusa los casos de uso que ya existen (retorno de un envío y recepción de un retiro), sin duplicar sus reglas.
/// </summary>
public sealed class RendirHojaDeRutaHandler(
    IHojaDeRutaRepository hojas,
    IEnvioRepository envios,
    IRetiroRepository retiros,
    RetornarADepositoHandler retornoEnvio,
    GestionarRetiroHandler gestionRetiro,
    ITenantContext tenant,
    IUnitOfWork unitOfWork,
    IRepartidorRepository repartidores)
{
    /// <summary>Lo mismo que <see cref="ObtenerAsync"/>, desde la app: solo la hoja del repartidor que inició sesión.</summary>
    public async Task<RendicionDto> ObtenerParaRepartidorAsync(Guid hojaDeRutaId, CancellationToken ct)
    {
        await VerificarQueEsDelRepartidorAsync(hojaDeRutaId, ct);
        return await ObtenerAsync(hojaDeRutaId, ct);
    }

    /// <summary>
    /// Primer paso (app): el repartidor rinde su hoja. Verifica lo escaneado contra lo que tiene que volver y deja la
    /// hoja pendiente de control. La hoja tiene control de concurrencia optimista: dos rendiciones a la vez, una gana.
    /// </summary>
    public async Task<ResultadoDeclaracion> DeclararParaRepartidorAsync(DeclararRendicionCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        await VerificarQueEsDelRepartidorAsync(command.HojaDeRutaId, ct);

        var (hoja, retornos, sinResultado) = await PrepararAsync(command.HojaDeRutaId, ct);

        if (hoja.Estado != EstadoHojaDeRuta.Despachada)
            throw new InvalidOperationException(
                hoja.Estado == EstadoHojaDeRuta.EnPreparacion
                    ? "Solo se puede rendir una hoja de ruta despachada."
                    : "Ya rendiste esta hoja de ruta.");

        VerificarSinParadasPendientes(sinResultado);
        VerificarEscaneo(retornos, command.BultosEscaneados);

        hoja.DeclararRendicion();
        await unitOfWork.GuardarCambiosAsync(ct);

        return new ResultadoDeclaracion(retornos.Sum(r => r.Bultos.Count));
    }

    public async Task<RendicionDto> ObtenerAsync(Guid hojaDeRutaId, CancellationToken ct)
    {
        var (hoja, retornos, sinResultado) = await PrepararAsync(hojaDeRutaId, ct);
        return new RendicionDto(hoja.Id, hoja.Estado.ToString(), retornos, sinResultado);
    }

    public async Task<ResultadoRendicion> Handle(RendirHojaDeRutaCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (string.IsNullOrWhiteSpace(command.Responsable))
            throw new ArgumentException("Debe identificarse quién recibe en el depósito.");

        var (hoja, retornos, sinResultado) = await PrepararAsync(command.HojaDeRutaId, ct);

        // El depósito controla lo que el repartidor ya rindió desde la app.
        if (hoja.Estado != EstadoHojaDeRuta.PendienteDeControl)
            throw new InvalidOperationException(
                hoja.Estado == EstadoHojaDeRuta.Rendida
                    ? "La rendición de esta hoja ya fue controlada."
                    : "El repartidor todavía no rindió esta hoja desde la app.");

        VerificarSinParadasPendientes(sinResultado);
        VerificarEscaneo(retornos, command.BultosEscaneados);

        var responsable = command.Responsable.Trim();
        var enviosRetornados = 0;
        var retirosRecibidos = 0;

        // El control es todo o nada, en una sola transacción. Primero se cierra la hoja: tiene control de concurrencia
        // optimista (xmin), así que si dos personas del depósito controlan a la vez, la segunda recibe 409 en este
        // primer guardado y no se aplica nada de lo suyo. Si después falla un retorno, se deshace todo.
        await unitOfWork.EjecutarEnTransaccionAsync(async transaccionCt =>
        {
            hoja.MarcarRendida();
            await unitOfWork.GuardarCambiosAsync(transaccionCt);

            foreach (var retorno in retornos)
            {
                if (retorno.Tipo == "Envio")
                {
                    await retornoEnvio.HandleDesdeRendicionAsync(
                        new RetornarADepositoCommand(retorno.Id, retorno.Bultos, responsable), transaccionCt);
                    enviosRetornados++;
                }
                else
                {
                    await gestionRetiro.RecibirEnDepositoAsync(retorno.Id, transaccionCt);
                    retirosRecibidos++;
                }
            }
        }, ct);

        return new ResultadoRendicion(enviosRetornados, retirosRecibidos);
    }

    // Una parada sin resultado quiere decir que el repartidor no la registró (o no sincronizó): no se cierra a ciegas.
    private static void VerificarSinParadasPendientes(IReadOnlyList<string> sinResultado)
    {
        if (sinResultado.Count > 0)
            throw new InvalidOperationException(
                $"Hay paradas sin resultado: {string.Join(", ", sinResultado)}. Sincronizá la app antes de rendir.");
    }

    private static void VerificarEscaneo(IEnumerable<RetornoEsperadoDto> retornos, IReadOnlyList<string>? escaneados)
    {
        var carga = VerificacionDeCarga.Verificar(retornos.SelectMany(r => r.Bultos), escaneados ?? []);
        if (carga.Completa)
            return;

        var partes = new List<string>();
        if (carga.Faltantes.Count > 0)
            partes.Add($"no volvieron {string.Join(", ", carga.Faltantes)} (si se perdieron, declarar el extravío del envío)");
        if (carga.Sobrantes.Count > 0)
            partes.Add($"sobran {string.Join(", ", carga.Sobrantes)}");

        throw new InvalidOperationException(
            $"La rendición no coincide con la hoja de ruta: {string.Join("; ", partes)}.");
    }

    private async Task VerificarQueEsDelRepartidorAsync(Guid hojaDeRutaId, CancellationToken ct)
    {
        var usuarioId = tenant.UsuarioId
            ?? throw new UnauthorizedAccessException("Hay que iniciar sesión.");
        var operadorId = tenant.OperadorId
            ?? throw new UnauthorizedAccessException("Primero hay que elegir un operador.");

        var repartidor = await repartidores.ObtenerPorUsuarioAsync(operadorId, usuarioId, ct)
            ?? throw new NoEncontradoException("Tu usuario no está vinculado a ningún repartidor de este operador.");

        var hoja = await hojas.ObtenerAsync(operadorId, hojaDeRutaId, ct)
            ?? throw new NoEncontradoException("La hoja de ruta no existe.");

        if (hoja.RepartidorId != repartidor.Id)
            throw new UnauthorizedAccessException("La hoja de ruta es de otro repartidor.");
    }

    private async Task<(HojaDeRuta Hoja, List<RetornoEsperadoDto> Retornos, List<string> SinResultado)> PrepararAsync(
        Guid hojaDeRutaId,
        CancellationToken ct)
    {
        var operadorId = tenant.OperadorId
            ?? throw new UnauthorizedAccessException("Primero hay que elegir un operador.");

        var hoja = await hojas.ObtenerAsync(operadorId, hojaDeRutaId, ct)
            ?? throw new NoEncontradoException("La hoja de ruta no existe.");

        var retornos = new List<RetornoEsperadoDto>();
        var sinResultado = new List<string>();

        foreach (var parada in hoja.Paradas.OrderBy(p => p.Orden))
        {
            if (parada.EnvioId is Guid envioId && parada.TipoOperacion == TipoOperacionRuta.Entrega)
            {
                var envio = await envios.ObtenerAsync(envioId, ct);
                if (envio is null)
                    continue;

                if (parada.Activa)
                {
                    sinResultado.Add(envio.ReferenciaExterna);
                    continue;
                }

                // Lo que no se entregó (todo o una parte) tiene que volver físicamente.
                if (envio.Estado is EstadoEnvio.NoEntregado or EstadoEnvio.EntregadoParcialmente)
                {
                    retornos.Add(new RetornoEsperadoDto(
                        "Envio",
                        envio.Id,
                        envio.ReferenciaExterna,
                        envio.Bultos
                            .Where(b => b.Estado == EstadoBulto.Pendiente)
                            .Select(b => b.Identificacion)
                            .OrderBy(i => i)
                            .ToList()));
                }
            }
            else if (parada.RetiroId is Guid retiroId && parada.TipoOperacion == TipoOperacionRuta.Retiro)
            {
                var retiro = await retiros.ObtenerAsync(retiroId, ct);
                if (retiro is null)
                    continue;

                var referencia = $"Retiro {retiro.Id.ToString()[..8].ToUpperInvariant()}";

                if (parada.Activa)
                {
                    sinResultado.Add(referencia);
                    continue;
                }

                // Los bultos retirados en domicilios entran al depósito (RF 38: Retirado → En depósito).
                if (retiro.Estado == EstadoRetiro.Retirado)
                {
                    retornos.Add(new RetornoEsperadoDto(
                        "Retiro",
                        retiro.Id,
                        referencia,
                        retiro.Bultos.Select(b => b.Identificacion).OrderBy(i => i).ToList()));
                }
            }
            else if (parada.Activa)
            {
                // Devoluciones o entregas al comercio que todavía no se confirmaron.
                sinResultado.Add($"parada {parada.Orden}");
            }
        }

        return (hoja, retornos, sinResultado);
    }
}
