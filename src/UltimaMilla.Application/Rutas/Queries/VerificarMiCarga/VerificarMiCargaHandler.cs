using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Common;
using UltimaMilla.Application.Seguridad;
using UltimaMilla.Domain.Envios;
using UltimaMilla.Domain.Retiros;
using UltimaMilla.Domain.Rutas;

namespace UltimaMilla.Application.Rutas.Queries.VerificarMiCarga;

/// <summary>
/// El repartidor escaneó los bultos al cargar el vehículo (RF 19) y pide verificarlos contra la hoja.
/// </summary>
public sealed record VerificarMiCargaQuery(
    Guid HojaDeRutaId,
    IReadOnlyList<string> BultosEscaneados);

/// <summary>
/// RF 19: verificación de la carga del vehículo contra la hoja de ruta, con aviso de faltantes y sobrantes.
/// La hoja ya la despachó el despachador desde el Backoffice (RF 17); esto no cambia ningún estado:
/// solo compara lo escaneado con lo que el servidor dice que tiene que subir al vehículo ahora
/// (si el despachador cambió algo después de que el teléfono bajó la hoja, se nota acá).
/// </summary>
public sealed class VerificarMiCargaHandler(
    IRepartidorRepository repartidores,
    IHojaDeRutaRepository hojas,
    IEnvioRepository envios,
    IRetiroRepository retiros,
    ITenantContext sesion)
{
    public async Task<ResultadoCarga> Handle(VerificarMiCargaQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var usuarioId = sesion.UsuarioId
            ?? throw new UnauthorizedAccessException("Hay que iniciar sesión.");

        var operadorId = sesion.OperadorId
            ?? throw new InvalidOperationException("Primero hay que elegir un operador.");

        var repartidor = await repartidores.ObtenerPorUsuarioAsync(operadorId, usuarioId, ct)
            ?? throw new NoEncontradoException(
                "Tu usuario no está vinculado a ningún repartidor de este operador.");

        var hoja = await hojas.ObtenerAsync(operadorId, query.HojaDeRutaId, ct)
            ?? throw new NoEncontradoException("La hoja de ruta no existe.");

        if (hoja.RepartidorId != repartidor.Id)
            throw new UnauthorizedAccessException("La hoja de ruta es de otro repartidor.");

        if (hoja.Estado == EstadoHojaDeRuta.EnPreparacion)
            throw new InvalidOperationException(
                "El despachador todavía no despachó esta hoja de ruta.");

        if (hoja.Estado is EstadoHojaDeRuta.PendienteDeControl or EstadoHojaDeRuta.Rendida)
            throw new InvalidOperationException("La hoja de ruta ya fue rendida.");

        var esperados = new List<string>();

        foreach (var parada in hoja.Paradas.Where(p => p.Activa))
        {
            switch (parada.TipoOperacion)
            {
                case TipoOperacionRuta.Entrega:
                case TipoOperacionRuta.DevolucionComercio:
                {
                    if (parada.EnvioId is not Guid envioId)
                        continue;

                    var envio = await envios.ObtenerAsync(envioId, ct);
                    if (envio is null || envio.OperadorId != operadorId)
                        continue;

                    // Entrega: lo despachado (En tránsito) con sus bultos pendientes (RF 32).
                    // Devolución al comercio: los bultos que vuelven a su dueño.
                    if (parada.TipoOperacion == TipoOperacionRuta.Entrega && envio.Estado == EstadoEnvio.EnTransito)
                    {
                        esperados.AddRange(envio.Bultos
                            .Where(b => b.Estado == EstadoBulto.Pendiente)
                            .Select(b => b.Identificacion));
                    }
                    else if (parada.TipoOperacion == TipoOperacionRuta.DevolucionComercio &&
                             envio.Estado == EstadoEnvio.EnDevolucion)
                    {
                        esperados.AddRange(envio.Bultos
                            .Where(b => b.Estado == EstadoBulto.EnDevolucion)
                            .Select(b => b.Identificacion));
                    }

                    break;
                }

                case TipoOperacionRuta.EntregaRetiroComercio:
                {
                    // Un retiro ya recibido en el depósito que se le lleva al comercio: sus bultos suben al vehículo.
                    if (parada.RetiroId is not Guid retiroId)
                        continue;

                    var retiro = await retiros.ObtenerAsync(retiroId, ct);
                    if (retiro is not null && retiro.OperadorId == operadorId && retiro.Estado == EstadoRetiro.EnDeposito)
                        esperados.AddRange(retiro.Bultos.Select(b => b.Identificacion));

                    break;
                }

                // RF 39: los bultos de un retiro en domicilio todavía no están en el depósito.
                case TipoOperacionRuta.Retiro:
                    break;
            }
        }

        return VerificacionDeCarga.Verificar(esperados, query.BultosEscaneados ?? []);
    }
}
