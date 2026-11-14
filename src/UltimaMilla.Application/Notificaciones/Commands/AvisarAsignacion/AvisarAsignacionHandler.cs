using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Seguridad;

using UltimaMilla.Domain.Rutas;

namespace UltimaMilla.Application.Notificaciones.Commands.AvisarAsignacion;

/// <param name="EnvioId">El envío que se acaba de asignar a una hoja de ruta.</param>
/// <param name="AsignadoEn">Cuándo se asignó (viene en el evento): si fue antes del despacho, no es urgente.</param>
public sealed record AvisarAsignacionCommand(Guid EnvioId, DateTimeOffset? AsignadoEn = null);

/// <summary>Cuántos teléfonos del repartidor recibieron el aviso.</summary>
public sealed record ResultadoAvisoAsignacion(int Dispositivos, int Enviadas, int TokensEliminados);

/// <summary>
/// Push al repartidor cuando le asignan un envío (reasignación urgente, requisito de la app móvil y
/// opcional 7.3): le llega aunque tenga la app cerrada, para que actualice la ruta antes de salir.
/// Lo llama el Worker al consumir EstadoEnvioCambiado, con el operador del mensaje ya fijado en el
/// contexto (ADR-002): los filtros por operador funcionan igual que en la API, sin IgnoreQueryFilters.
/// </summary>
public sealed class AvisarAsignacionHandler(
    IHojaDeRutaRepository hojas,
    IRepartidorRepository repartidores,
    IEnvioRepository envios,
    IDispositivoMovilRepository dispositivos,
    INotificadorPush notificador,
    ITenantContext tenant,
    IUnitOfWork unitOfWork)
{
    public async Task<ResultadoAvisoAsignacion> Handle(AvisarAsignacionCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        var operadorId = tenant.OperadorId
            ?? throw new InvalidOperationException("El mensaje no trae operador.");

        var sinAviso = new ResultadoAvisoAsignacion(0, 0, 0);

        // Si entre la asignación y este mensaje lo volvieron a quitar, ya no hay nada que avisar.
        var hoja = await hojas.ObtenerPorEnvioActivoAsync(operadorId, command.EnvioId, ct);
        if (hoja is null)
            return sinAviso;

        // El push es para la reasignación urgente (letra 6.11): un envío que se suma a una hoja que ya salió.
        // Una hoja en preparación todavía no le llega al repartidor (RF 17): la ve completa cuando se despacha.
        if (hoja.Estado != EstadoHojaDeRuta.Despachada)
            return sinAviso;

        // El mensaje de una asignación hecha antes del despacho puede procesarse después: tampoco es urgente.
        if (command.AsignadoEn is { } asignadoEn && hoja.DespachadaEn is { } despachadaEn && asignadoEn < despachadaEn)
            return sinAviso;

        var repartidor = await repartidores.ObtenerAsync(operadorId, hoja.RepartidorId, ct);
        if (repartidor?.UsuarioId is not Guid usuarioId)
            return sinAviso; // repartidor sin usuario de la app: no tiene teléfono donde avisarle

        var telefonos = await dispositivos.ListarPorUsuarioAsync(usuarioId, ct);
        if (telefonos.Count == 0)
            return sinAviso;

        var envio = await envios.ObtenerAsync(command.EnvioId, ct);
        var detalle = envio is null
            ? "Pasá por el depósito a buscarlo y actualizá tu ruta en la app."
            : $"{envio.ReferenciaExterna} · {envio.Direccion.Calle} {envio.Direccion.Numero}, {envio.Direccion.Localidad}. Pasá por el depósito a buscarlo y actualizá tu ruta.";

        var notificacion = new NotificacionPush(
            "Envío urgente en tu ruta",
            detalle,
            new Dictionary<string, string>
            {
                ["tipo"] = "asignacion",
                ["envioId"] = command.EnvioId.ToString(),
                ["hojaDeRutaId"] = hoja.Id.ToString()
            });

        var enviadas = 0;
        var eliminados = 0;

        foreach (var telefono in telefonos)
        {
            switch (await notificador.EnviarAsync(telefono.Token, notificacion, ct))
            {
                case ResultadoPush.Enviado:
                    enviadas++;
                    break;

                // Ciclo de vida del identificador: un token que Firebase ya no reconoce se da de baja.
                case ResultadoPush.TokenInvalido:
                    dispositivos.Eliminar(telefono);
                    eliminados++;
                    break;

                // Sin Firebase configurado (por ejemplo en las pruebas locales) no se avisa, pero no es un error del mensaje.
                case ResultadoPush.NoConfigurado:
                    return sinAviso;
            }
        }

        if (eliminados > 0)
            await unitOfWork.GuardarCambiosAsync(ct);

        return new ResultadoAvisoAsignacion(telefonos.Count, enviadas, eliminados);
    }
}
