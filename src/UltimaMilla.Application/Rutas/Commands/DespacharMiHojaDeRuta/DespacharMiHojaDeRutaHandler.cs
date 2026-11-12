
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Common;
using UltimaMilla.Application.Rutas.Commands.DespacharHojaDeRuta;
using UltimaMilla.Application.Seguridad;
using UltimaMilla.Domain.Envios;
using UltimaMilla.Domain.Retiros;
using UltimaMilla.Domain.Rutas;

namespace UltimaMilla.Application.Rutas.Commands.DespacharMiHojaDeRuta;

public sealed class DespacharMiHojaDeRutaHandler(
    IRepartidorRepository repartidores,
    IHojaDeRutaRepository hojas,
    IEnvioRepository envios,
    IRetiroRepository retiros,
    ITenantContext sesion,
    DespacharHojaDeRutaHandler despacho)
{
    public async Task<int> Handle(
        DespacharMiHojaDeRutaCommand command,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        var usuarioId = sesion.UsuarioId
            ?? throw new UnauthorizedAccessException(
                "Hay que iniciar sesión.");

        var operadorId = sesion.OperadorId
            ?? throw new InvalidOperationException(
                "Primero hay que elegir un operador.");

        var repartidor = await repartidores.ObtenerPorUsuarioAsync(
            operadorId, usuarioId, ct)
            ?? throw new NoEncontradoException(
                "Tu usuario no está vinculado a ningún repartidor de este operador.");

        var hoja = await hojas.ObtenerAsync(
            operadorId, command.HojaDeRutaId, ct)
            ?? throw new NoEncontradoException(
                "La hoja de ruta no existe.");

        if (hoja.RepartidorId != repartidor.Id)
        {
            throw new UnauthorizedAccessException(
                "La hoja de ruta es de otro repartidor.");
        }

        // RF32: solo se cargan los bultos pendientes de los envíos.
        // RF39: los bultos de los retiros todavía están en los domicilios.
        var esperados = new List<string>();

        foreach (var parada in hoja.Paradas.Where(p => p.Activa))
        {
            switch (parada.TipoOperacion)
            {
                case TipoOperacionRuta.Entrega:
                case TipoOperacionRuta.DevolucionComercio:
                {
                    var envioId = parada.EnvioId
                        ?? throw new InvalidOperationException(
                            "La parada no tiene un envío asociado.");

                    var envio = await envios.ObtenerAsync(envioId, ct)
                        ?? throw new NoEncontradoException(
                            $"No existe el envío de la parada {parada.Orden}.");

                    if (envio.OperadorId != operadorId)
                        throw new InvalidOperationException(
                            "El envío pertenece a otro operador.");

                    if (parada.TipoOperacion == TipoOperacionRuta.Entrega)
                    {
                        if (envio.Estado != EstadoEnvio.AsignadoARuta)
                            throw new InvalidOperationException(
                                "El envío no está asignado a ruta.");

                        esperados.AddRange(
                            envio.Bultos
                                .Where(b => b.Estado == EstadoBulto.Pendiente)
                                .Select(b => b.Identificacion));
                    }
                    else
                    {
                        if (envio.Estado != EstadoEnvio.EnDevolucion)
                            throw new InvalidOperationException(
                                "El envío no está en devolución.");

                        esperados.AddRange(
                            envio.Bultos
                                .Where(b => b.Estado == EstadoBulto.EnDevolucion)
                                .Select(b => b.Identificacion));
                    }

                    break;
                }

                case TipoOperacionRuta.Retiro:
                case TipoOperacionRuta.EntregaRetiroComercio:
                {
                    var retiroId = parada.RetiroId
                        ?? throw new InvalidOperationException(
                            "La parada no tiene un retiro asociado.");

                    var retiro = await retiros.ObtenerAsync(retiroId, ct)
                        ?? throw new NoEncontradoException(
                            $"No existe el retiro de la parada {parada.Orden}.");

                    if (retiro.OperadorId != operadorId)
                        throw new InvalidOperationException(
                            "El retiro pertenece a otro operador.");

                    if (parada.TipoOperacion == TipoOperacionRuta.Retiro)
                    {
                        if (retiro.HojaDeRutaId != hoja.Id ||
                            retiro.Estado != EstadoRetiro.AsignadoARuta)
                        {
                            throw new InvalidOperationException(
                                "El retiro no está correctamente asignado.");
                        }

                        // Se recoge durante el recorrido.
                        // No se carga al salir del depósito.
                    }
                    else
                    {
                        if (retiro.Estado != EstadoRetiro.EnDeposito)
                            throw new InvalidOperationException(
                                "El retiro todavía no está en depósito.");

                        // Este retiro ya fue recogido.
                        // Sus bultos deben cargarse al salir.
                        esperados.AddRange(
                            retiro.Bultos.Select(b => b.Identificacion));
                    }

                    break;
                }

                default:
                    throw new InvalidOperationException(
                        "La parada tiene un tipo de operación inválido.");
            }
        }

        var carga = VerificacionDeCarga.Verificar(
            esperados,
            command.BultosEscaneados ?? []);

        if (!carga.Completa)
        {
            var partes = new List<string>();

            if (carga.Faltantes.Count > 0)
            {
                partes.Add(
                    $"faltan {string.Join(", ", carga.Faltantes)}");
            }

            if (carga.Sobrantes.Count > 0)
            {
                partes.Add(
                    $"sobran {string.Join(", ", carga.Sobrantes)}");
            }

            throw new InvalidOperationException(
                $"La carga no coincide con la hoja de ruta: {string.Join("; ", partes)}.");
        }

        // Reutilizar el despacho centralizado.
        return await despacho.Handle(
            new DespacharHojaDeRutaCommand(
                operadorId,
                hoja.Id),
            ct);
    }
}
