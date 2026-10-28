using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Common;
using UltimaMilla.Application.Rutas.Commands.DespacharHojaDeRuta;
using UltimaMilla.Application.Seguridad;

namespace UltimaMilla.Application.Rutas.Commands.DespacharMiHojaDeRuta;

/// <summary>
/// Despacho desde la app: en la spec, la transición Despachar la hace el repartidor y exige
/// "bulto escaneado al cargar el vehículo". Este Handler controla eso y después usa el
/// despacho que ya existe (DespacharHojaDeRutaHandler), sin duplicar su lógica.
/// </summary>
public sealed class DespacharMiHojaDeRutaHandler(
    IRepartidorRepository repartidores,
    IHojaDeRutaRepository hojas,
    IEnvioRepository envios,
    ITenantContext sesion,
    DespacharHojaDeRutaHandler despacho)
{
    public async Task<int> Handle(DespacharMiHojaDeRutaCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        var usuarioId = sesion.UsuarioId
            ?? throw new UnauthorizedAccessException("Hay que iniciar sesión.");

        var operadorId = sesion.OperadorId
            ?? throw new InvalidOperationException("Primero hay que elegir un operador.");

        var repartidor = await repartidores.ObtenerPorUsuarioAsync(operadorId, usuarioId, ct)
            ?? throw new NoEncontradoException(
                "Tu usuario no está vinculado a ningún repartidor de este operador.");

        var hoja = await hojas.ObtenerAsync(operadorId, command.HojaDeRutaId, ct)
            ?? throw new NoEncontradoException("La hoja de ruta no existe.");

        // Un repartidor solo despacha SU hoja.
        if (hoja.RepartidorId != repartidor.Id)
            throw new UnauthorizedAccessException("La hoja de ruta es de otro repartidor.");

        // Los bultos que tienen que subir al vehículo: los de los envíos de las paradas activas.
        var esperados = new List<string>();
        foreach (var parada in hoja.Paradas.Where(p => p.Activa))
        {
            var envio = await envios.ObtenerAsync(parada.EnvioId, ct)
                ?? throw new NoEncontradoException($"No existe el envío de la parada {parada.Orden}.");

            esperados.AddRange(envio.Bultos.Select(b => b.Identificacion));
        }

        var carga = VerificacionDeCarga.Verificar(esperados, command.BultosEscaneados ?? []);

        // RF 19: aviso ante faltantes o sobrantes. La app ya lo controla, pero el servidor no confía en eso.
        if (!carga.Completa)
        {
            var partes = new List<string>();
            if (carga.Faltantes.Count > 0)
                partes.Add($"faltan {string.Join(", ", carga.Faltantes)}");
            if (carga.Sobrantes.Count > 0)
                partes.Add($"sobran {string.Join(", ", carga.Sobrantes)}");

            throw new InvalidOperationException(
                $"La carga no coincide con la hoja de ruta: {string.Join("; ", partes)}.");
        }

        return await despacho.Handle(new DespacharHojaDeRutaCommand(operadorId, hoja.Id), ct);
    }
}
