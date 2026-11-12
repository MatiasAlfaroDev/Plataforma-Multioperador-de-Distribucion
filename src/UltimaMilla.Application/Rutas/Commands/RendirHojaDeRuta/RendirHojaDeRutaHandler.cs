using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Common;
using UltimaMilla.Application.Envios.Commands.RetornarADeposito;
using UltimaMilla.Application.Retiros.Commands.GestionarRetiro;
using UltimaMilla.Application.Seguridad;
using UltimaMilla.Domain.Envios;
using UltimaMilla.Domain.Retiros;
using UltimaMilla.Domain.Rutas;

namespace UltimaMilla.Application.Rutas.Commands.RendirHojaDeRuta;

/// <param name="BultosEscaneados">Lo que el repartidor trae de vuelta, escaneado en el depósito.</param>
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

/// <summary>
/// RF 22: rendición al regresar al depósito. Compara lo que tiene que volver (bultos no entregados y
/// bultos retirados en domicilios) con lo escaneado al llegar, igual que la carga del vehículo pero al revés.
/// Si coincide, cada envío vuelve "En depósito" y cada retiro queda "En depósito", y la hoja se cierra.
/// Reusa los casos de uso que ya existen (retorno de un envío y recepción de un retiro), sin duplicar sus reglas.
/// </summary>
public sealed class RendirHojaDeRutaHandler(
    IHojaDeRutaRepository hojas,
    IEnvioRepository envios,
    IRetiroRepository retiros,
    RetornarADepositoHandler retornoEnvio,
    GestionarRetiroHandler gestionRetiro,
    ITenantContext tenant,
    IUnitOfWork unitOfWork)
{
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

        if (hoja.Estado != EstadoHojaDeRuta.Despachada)
            throw new InvalidOperationException(
                hoja.Estado == EstadoHojaDeRuta.Rendida
                    ? "La hoja de ruta ya fue rendida."
                    : "Solo se puede rendir una hoja de ruta despachada.");

        // Una parada sin resultado quiere decir que el repartidor no la registró (o no sincronizó): no se cierra a ciegas.
        if (sinResultado.Count > 0)
            throw new InvalidOperationException(
                $"Hay paradas sin resultado: {string.Join(", ", sinResultado)}. Sincronizá la app antes de rendir.");

        var carga = VerificacionDeCarga.Verificar(retornos.SelectMany(r => r.Bultos), command.BultosEscaneados ?? []);
        if (!carga.Completa)
        {
            var partes = new List<string>();
            if (carga.Faltantes.Count > 0)
                partes.Add($"no volvieron {string.Join(", ", carga.Faltantes)} (si se perdieron, declarar el extravío del envío)");
            if (carga.Sobrantes.Count > 0)
                partes.Add($"sobran {string.Join(", ", carga.Sobrantes)}");

            throw new InvalidOperationException(
                $"La rendición no coincide con la hoja de ruta: {string.Join("; ", partes)}.");
        }

        var responsable = command.Responsable.Trim();
        var enviosRetornados = 0;
        var retirosRecibidos = 0;

        // Cada retorno se guarda con su propio caso de uso. Si algo falla a mitad de camino, volver a rendir
        // retoma desde donde quedó: lo que ya está en depósito deja de figurar como "a devolver".
        foreach (var retorno in retornos)
        {
            if (retorno.Tipo == "Envio")
            {
                await retornoEnvio.Handle(new RetornarADepositoCommand(retorno.Id, retorno.Bultos, responsable), ct);
                enviosRetornados++;
            }
            else
            {
                await gestionRetiro.RecibirEnDepositoAsync(retorno.Id, ct);
                retirosRecibidos++;
            }
        }

        hoja.MarcarRendida();
        await unitOfWork.GuardarCambiosAsync(ct);

        return new ResultadoRendicion(enviosRetornados, retirosRecibidos);
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
