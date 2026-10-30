using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Common;
using UltimaMilla.Application.Seguridad;

namespace UltimaMilla.Application.Rutas.Queries.ObtenerMisHojasDeRuta;

public sealed class ObtenerMisHojasDeRutaHandler(
    IRepartidorRepository repartidores,
    IHojaDeRutaRepository hojas,
    IEnvioRepository envios,
    ITenantContext sesion)
{
    public async Task<IReadOnlyList<MiHojaDeRutaDto>> Handle(
        ObtenerMisHojasDeRutaQuery query,
        CancellationToken ct)
    {
        var usuarioId = sesion.UsuarioId
            ?? throw new UnauthorizedAccessException("Hay que iniciar sesión.");

        var operadorId = sesion.OperadorId
            ?? throw new InvalidOperationException("Primero hay que elegir un operador.");

        // El usuario de la app es un repartidor solo si está vinculado a uno de este operador.
        var repartidor = await repartidores.ObtenerPorUsuarioAsync(operadorId, usuarioId, ct)
            ?? throw new NoEncontradoException(
                "Tu usuario no está vinculado a ningún repartidor de este operador.");

        var delDia = await hojas.ListarDelRepartidorAsync(operadorId, repartidor.Id, query.Fecha, ct);

        var resultado = new List<MiHojaDeRutaDto>();

        foreach (var hoja in delDia)
        {
            var paradas = new List<MiParadaDto>();

            // Solo las paradas activas, en el orden en que se recorren.
            foreach (var parada in hoja.Paradas.Where(p => p.Activa).OrderBy(p => p.Orden))
            {
                var envio = await envios.ObtenerAsync(parada.EnvioId, ct);
                if (envio is null)
                    continue;

                paradas.Add(new MiParadaDto(
                    parada.Orden,
                    envio.Id,
                    envio.ReferenciaExterna,
                    envio.Destinatario.Nombre,
                    envio.Destinatario.Telefono,
                    $"{envio.Direccion.Calle} {envio.Direccion.Numero}",
                    envio.Direccion.Localidad,
                    envio.Estado.ToString(),
                    envio.Bultos.Select(b => b.Identificacion).OrderBy(i => i).ToList()));
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
