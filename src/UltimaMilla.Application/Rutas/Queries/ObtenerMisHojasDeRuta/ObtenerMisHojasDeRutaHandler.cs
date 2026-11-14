
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Common;
using UltimaMilla.Application.Seguridad;
using UltimaMilla.Domain.Envios;
using UltimaMilla.Domain.Retiros;
using UltimaMilla.Domain.Rutas;

namespace UltimaMilla.Application.Rutas.Queries.ObtenerMisHojasDeRuta;

public sealed class ObtenerMisHojasDeRutaHandler(
    IRepartidorRepository repartidores,
    IHojaDeRutaRepository hojas,
    IEnvioRepository envios,
    IRetiroRepository retiros,
    ICuentaComercialRepository cuentas,
    ITenantContext sesion)
{
    public async Task<IReadOnlyList<MiHojaDeRutaDto>> Handle(
        ObtenerMisHojasDeRutaQuery query,
        CancellationToken ct)
    {
        var usuarioId = sesion.UsuarioId
            ?? throw new UnauthorizedAccessException(
                "Hay que iniciar sesión.");

        var operadorId = sesion.OperadorId
            ?? throw new InvalidOperationException(
                "Primero hay que elegir un operador.");

        var repartidor = await repartidores.ObtenerPorUsuarioAsync(
            operadorId, usuarioId, ct)
            ?? throw new NoEncontradoException(
                "Tu usuario no está vinculado a un repartidor de este operador.");

        var delDia = await hojas.ListarDelRepartidorAsync(
            operadorId,
            repartidor.Id,
            query.Fecha,
            ct);

        var resultado = new List<MiHojaDeRutaDto>();

        // RF 17: la hoja llega a la app recién cuando el despachador la despacha desde el Backoffice.
        // Una hoja en preparación todavía puede cambiar: el repartidor no la ve.
        foreach (var hoja in delDia.Where(h => h.Estado != EstadoHojaDeRuta.EnPreparacion))
        {
            var paradas = new List<MiParadaDto>();

            // Las paradas activas y las que se resolvieron después del despacho (RF 22: lo que se entregó en
            // parte o no se entregó sigue a cargo del repartidor hasta rendir). Las quitadas antes del despacho no.
            foreach (var parada in hoja.Paradas
                .Where(p => p.Activa || (hoja.DespachadaEn is { } despachadaEn && p.LiberadaEn >= despachadaEn))
                .OrderBy(p => p.Orden))
            {
                switch (parada.TipoOperacion)
                {
                    case TipoOperacionRuta.Entrega:
                    case TipoOperacionRuta.DevolucionComercio:
                    {
                        var envioId = parada.EnvioId
                            ?? throw new InvalidOperationException(
                                "La parada no tiene envío asociado.");

                        var envio = await envios.ObtenerAsync(envioId, ct)
                            ?? throw new InvalidOperationException(
                                $"No existe el envío {envioId} de la ruta.");

                        if (envio.OperadorId != operadorId)
                            throw new InvalidOperationException(
                                "La hoja contiene un envío de otro operador.");

                        var esDevolucion =
                            parada.TipoOperacion ==
                            TipoOperacionRuta.DevolucionComercio;

                        // Una parada resuelta se muestra solo con el resultado de la visita.
                        if (!parada.Activa &&
                            (esDevolucion || envio.Estado is not (EstadoEnvio.Entregado
                                or EstadoEnvio.EntregadoParcialmente or EstadoEnvio.NoEntregado)))
                        {
                            break;
                        }

                        // Resuelta: no queda nada que cargar ni escanear en la parada.
                        List<Bulto> bultos = parada.Activa
                            ? envio.Bultos
                                .Where(b => b.Estado ==
                                    (esDevolucion
                                        ? EstadoBulto.EnDevolucion
                                        : EstadoBulto.Pendiente))
                                .OrderBy(b => b.Identificacion)
                                .ToList()
                            : [];

                        var direccion = envio.Direccion;
                        var nombre = envio.Destinatario.Nombre;
                        string? telefono = envio.Destinatario.Telefono;

                        if (esDevolucion)
                        {
                            var cuenta = await cuentas.ObtenerAsync(
                                envio.CuentaComercialId, ct)
                                ?? throw new InvalidOperationException(
                                    "No existe la cuenta comercial del envío.");

                            if (cuenta.OperadorId != operadorId)
                                throw new InvalidOperationException(
                                    "La cuenta pertenece a otro operador.");

                            direccion = cuenta.DireccionRecepcion
                                ?? throw new InvalidOperationException(
                                    "El comercio no tiene dirección de recepción.");

                            nombre = "Devolución al comercio";
                            telefono = null;
                        }

                        paradas.Add(new MiParadaDto(
                            parada.Orden,
                            envio.Id,
                            envio.ReferenciaExterna,
                            nombre,
                            telefono,
                            $"{direccion.Calle} {direccion.Numero}",
                            direccion.Localidad,
                            envio.Estado.ToString(),
                            bultos.Select(b => b.Identificacion).ToList(),
                            bultos.Select(b => new MiBultoDto(
                                b.Id,
                                b.Identificacion)).ToList(),
                            TipoOperacion: parada.TipoOperacion.ToString(),
                            Activa: parada.Activa));

                        break;
                    }

                    case TipoOperacionRuta.Retiro:
                    case TipoOperacionRuta.EntregaRetiroComercio:
                    {
                        var retiroId = parada.RetiroId
                            ?? throw new InvalidOperationException(
                                "La parada no tiene retiro asociado.");

                        var retiro = await retiros.ObtenerAsync(retiroId, ct)
                            ?? throw new InvalidOperationException(
                                $"No existe el retiro {retiroId} de la ruta.");

                        if (retiro.OperadorId != operadorId)
                            throw new InvalidOperationException(
                                "La hoja contiene un retiro de otro operador.");

                        var esRecogida =
                            parada.TipoOperacion == TipoOperacionRuta.Retiro;

                        if (!parada.Activa &&
                            (!esRecogida || retiro.Estado is not (EstadoRetiro.Retirado or EstadoRetiro.NoRetirado)))
                        {
                            break;
                        }

                        if (esRecogida && retiro.HojaDeRutaId != hoja.Id)
                            throw new InvalidOperationException(
                                "El retiro no pertenece a esta hoja.");

                        var direccion = esRecogida
                            ? retiro.DomicilioRetiro
                            : retiro.DomicilioDestino;

                        paradas.Add(new MiParadaDto(
                            parada.Orden,
                            null,
                            retiro.Id.ToString(),
                            esRecogida
                                ? "Retiro en domicilio"
                                : "Entrega de retiro al comercio",
                            null,
                            $"{direccion.Calle} {direccion.Numero}",
                            direccion.Localidad,
                            retiro.Estado.ToString(),
                            retiro.Bultos
                                .Select(b => b.Identificacion)
                                .OrderBy(x => x)
                                .ToList(),
                            // RF 40: la app escanea estos bultos al retirarlos en el domicilio.
                            retiro.Bultos
                                .Select(b => new MiBultoDto(b.Id, b.Identificacion))
                                .ToList(),
                            RetiroId: retiro.Id,
                            TipoOperacion: parada.TipoOperacion.ToString(),
                            Activa: parada.Activa));

                        break;
                    }

                    default:
                        throw new InvalidOperationException(
                            "La hoja contiene un tipo de parada inválido.");
                }
            }

            resultado.Add(new MiHojaDeRutaDto(
                hoja.Id,
                hoja.Fecha,
                hoja.HoraSalida,
                repartidor.Nombre,
                paradas,
                hoja.Estado.ToString()));
        }

        return resultado;
    }
}
