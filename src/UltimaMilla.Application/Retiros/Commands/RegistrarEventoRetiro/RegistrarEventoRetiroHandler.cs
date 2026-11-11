using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Common;
using UltimaMilla.Application.Seguridad;
using UltimaMilla.Domain.Retiros;
using UltimaMilla.Domain.Rutas;
using UltimaMilla.Application.Avisos.Commands.RegistrarAvisosRetiro;
using UltimaMilla.Domain.Avisos;

namespace UltimaMilla.Application.Retiros.Commands.RegistrarEventoRetiro;

public sealed class RegistrarEventoRetiroHandler(
    IRetiroRepository retiros,
    IHojaDeRutaRepository hojas,
    IRepartidorRepository repartidores,
    IEventoRetiroMovilRepository eventosMovil,
    IUnitOfWork unitOfWork,
    ITenantContext sesion,
    TimeProvider reloj,
    RegistrarAvisosRetiroHandler avisos)
{
    public async Task Handle(
        RegistrarEventoRetiroCommand command,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.IdEvento == Guid.Empty ||
            command.RetiroId == Guid.Empty)
            throw new ArgumentException(
                "Los identificadores son obligatorios.");

        var usuarioId = sesion.UsuarioId
            ?? throw new UnauthorizedAccessException(
                "Hay que iniciar sesión.");

        var operadorId = sesion.OperadorId
            ?? throw new UnauthorizedAccessException(
                "No hay un operador seleccionado.");

        var repartidor = await repartidores.ObtenerPorUsuarioAsync(
            operadorId, usuarioId, ct)
            ?? throw new UnauthorizedAccessException(
                "El usuario no está vinculado a un repartidor.");

        // Un mismo evento recibido dos veces no se vuelve a aplicar.
        if (await eventosMovil.ExisteAsync(command.IdEvento, ct))
            throw new ConflictoException(
                "El evento ya fue registrado.");

        var retiro = await retiros.ObtenerAsync(
            command.RetiroId, ct)
            ?? throw new NoEncontradoException(
                "El retiro no existe.");

        if (retiro.OperadorId != operadorId)
            throw new NoEncontradoException(
                "El retiro no existe.");

        if (retiro.Estado != EstadoRetiro.AsignadoARuta ||
            retiro.HojaDeRutaId is not Guid hojaId)
            throw new InvalidOperationException(
                "El retiro no está pendiente de ejecución.");

        var hoja = await hojas.ObtenerAsync(
            operadorId, hojaId, ct)
            ?? throw new NoEncontradoException(
                "La hoja de ruta no existe.");

        if (hoja.RepartidorId != repartidor.Id)
            throw new UnauthorizedAccessException(
                "El retiro pertenece a otro repartidor.");

        if (hoja.Estado != EstadoHojaDeRuta.Despachada)
            throw new InvalidOperationException(
                "La hoja de ruta todavía no fue despachada.");

        if (!hoja.Paradas.Any(p =>
            p.RetiroId == retiro.Id && p.Activa))
            throw new InvalidOperationException(
                "El retiro no tiene una parada activa.");

        var tipo = command.Tipo?.Trim();

        if (tipo != "Retirado" && tipo != "NoRetirado")
            throw new ArgumentException(
                "El tipo debe ser Retirado o NoRetirado.");

        if (tipo == "Retirado")
        {
            var identificaciones =
                command.IdentificacionesBultos?
                    .Select(x => x?.Trim())
                    .ToArray();

            if (identificaciones is null ||
                identificaciones.Length == 0 ||
                identificaciones.Any(string.IsNullOrWhiteSpace))
                throw new ArgumentException(
                    "Debe identificar todos los bultos.");

            var declarados = retiro.Bultos
                .Select(b => b.Identificacion)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var escaneados = identificaciones
                .Select(x => x!)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (escaneados.Count != identificaciones.Length ||
                !declarados.SetEquals(escaneados))
                throw new ArgumentException(
                    "Los bultos escaneados no coinciden " +
                    "con los declarados para el retiro.");
        }

        // Se valida y construye el evento antes
        // de cambiar el estado del retiro.
        var evento = EventoRetiroMovil.Crear(
            command.IdEvento,
            retiro,
            tipo,
            command.FechaHora,
            reloj.GetUtcNow(),
            command.IdentificacionesBultos,
            command.ReferenciaFotografia,
            command.ReferenciaFirma,
            command.MotivoFallo);

        if (tipo == "Retirado")
        {
            retiro.RegistrarRetiro(
                evento.OcurridoEn,
                evento.ReferenciaFotografia!,
                evento.ReferenciaFirma!);
        }
        else
        {
            retiro.RegistrarRetiroFallido();
        }

        hoja.FinalizarParadaRetiro(
            retiro, evento.OcurridoEn);

        await eventosMovil.AgregarAsync(evento, ct);

        var tipoAviso = tipo == "Retirado"
            ? TiposAviso.RetiroRetirado
            : TiposAviso.RetiroNoRetirado;

        await avisos.PrepararAsync(
            retiro.Id,
            tipoAviso,
            $"retiro:{retiro.Id}:evento:{evento.Id}",
            evento.OcurridoEn,
            ct);

        // Estado, parada y evento se persisten juntos.
        await unitOfWork.GuardarCambiosAsync(ct);
    }
}