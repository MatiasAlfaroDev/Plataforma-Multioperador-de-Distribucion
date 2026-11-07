
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Common;
using UltimaMilla.Application.Seguridad;
using UltimaMilla.Domain.Envios;

namespace UltimaMilla.Application.Rutas.Queries.ObtenerMisHojasDeRuta;

public sealed class ObtenerMisHojasDeRutaHandler(
    IRepartidorRepository repartidores,
    IHojaDeRutaRepository hojas,
    IEnvioRepository envios,
    IRetiroRepository retiros,
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

        var repartidor =
            await repartidores.ObtenerPorUsuarioAsync(
                operadorId, usuarioId, ct)
            ?? throw new NoEncontradoException(
                "Tu usuario no está vinculado a un repartidor de este operador.");

        var delDia = await hojas.ListarDelRepartidorAsync(
            operadorId,
            repartidor.Id,
            query.Fecha,
            ct);

        var resultado = new List<MiHojaDeRutaDto>();

        foreach (var hoja in delDia)
        {
            var paradas = new List<MiParadaDto>();

            foreach (var parada in hoja.Paradas
                .Where(p => p.Activa)
                .OrderBy(p => p.Orden))
            {
                if (parada.EnvioId is Guid envioId)
                {
                    var envio = await envios.ObtenerAsync(
                        envioId, ct)
                        ?? throw new InvalidOperationException(
                            $"No existe el envío {envioId} de la ruta.");
                    if (envio.OperadorId != operadorId)
                        throw new InvalidOperationException(
                            "La hoja contiene un envío de otro operador.");

                    // Solo los bultos pendientes de entrega.
                    var pendientes = envio.Bultos
                        .Where(b => b.Estado == EstadoBulto.Pendiente)
                        .OrderBy(b => b.Identificacion)
                        .ToList();

                    paradas.Add(new MiParadaDto(
                        parada.Orden,
                        envio.Id,
                        envio.ReferenciaExterna,
                        envio.Destinatario.Nombre,
                        envio.Destinatario.Telefono,
                        $"{envio.Direccion.Calle} {envio.Direccion.Numero}",
                        envio.Direccion.Localidad,
                        envio.Estado.ToString(),
                        pendientes
                            .Select(b => b.Identificacion)
                            .ToList(),
                        pendientes
                            .Select(b => new MiBultoDto(
                                b.Id,
                                b.Identificacion))
                            .ToList()));
                }
                else if (parada.RetiroId is Guid retiroId)
                {
                    var retiro = await retiros.ObtenerAsync(retiroId, ct)
                        ?? throw new InvalidOperationException(
                            $"No existe el retiro {retiroId} de la ruta.");

                    if (retiro.OperadorId != operadorId ||
                        retiro.HojaDeRutaId != hoja.Id)
                        throw new InvalidOperationException(
                            "La hoja contiene un retiro inválido.");

                    paradas.Add(new MiParadaDto(
                        parada.Orden,
                        null,
                        retiro.Id.ToString(),
                        "Retiro en domicilio",
                        null,
                        $"{retiro.DomicilioRetiro.Calle} {retiro.DomicilioRetiro.Numero}",
                        retiro.DomicilioRetiro.Localidad,
                        retiro.Estado.ToString(),
                        retiro.Bultos
                            .Select(b => b.Identificacion)
                            .OrderBy(x => x)
                            .ToList(),
                        RetiroId: retiro.Id,
                        TipoOperacion: "Retiro"));
                }
                else
                {
                    throw new InvalidOperationException(
                        "La hoja contiene una parada inválida.");
                }
            }

            resultado.Add(new MiHojaDeRutaDto(
                hoja.Id,
                hoja.Fecha,
                hoja.HoraSalida,
                repartidor.Nombre,
                paradas));
        }

        return resultado;
    }
}
