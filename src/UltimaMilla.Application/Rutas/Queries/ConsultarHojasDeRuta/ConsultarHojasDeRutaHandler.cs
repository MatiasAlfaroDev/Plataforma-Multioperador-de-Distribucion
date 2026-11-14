
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Domain.Envios;
using UltimaMilla.Domain.Configuracion;
using UltimaMilla.Domain.Rutas;
using UltimaMilla.Domain.Retiros;
using UltimaMilla.Domain.Organizacion;

namespace UltimaMilla.Application.Rutas.Queries.ConsultarHojasDeRuta;

public sealed record RepartidorRutaDto(
    Guid Id,
    string Nombre);

public sealed record VehiculoRutaDto(
    Guid Id,
    string Matricula,
    decimal CapacidadPesoKg,
    decimal CapacidadVolumenM3);

public sealed record EnvioRutaDto(
    Guid Id,
    string ReferenciaExterna,
    string Localidad,
    string Estado,
    decimal PesoKg,
    decimal VolumenM3,
    DateOnly? FechaComprometida,
    TimeOnly? HoraDesde,
    TimeOnly? HoraHasta,
    bool TieneCompromiso);

public sealed record RetiroRutaDto(
    Guid Id,
    string Motivo,
    string Localidad,
    string Estado,
    decimal PesoKg,
    decimal VolumenM3,
    DateOnly Fecha,
    TimeOnly HoraDesde,
    TimeOnly HoraHasta);

public sealed record DevolucionRutaDto(
    Guid Id,
    string ReferenciaExterna,
    string Localidad,
    string Estado,
    decimal PesoKg,
    decimal VolumenM3);

public sealed record EntregaRetiroRutaDto(
    Guid Id,
    string Motivo,
    string Localidad,
    string Estado,
    decimal PesoKg,
    decimal VolumenM3);

public sealed record FranjaRutaDto(
    TimeOnly HoraDesde,
    TimeOnly HoraHasta);

public sealed record OpcionesHojaDeRutaDto(
    IReadOnlyList<RepartidorRutaDto> Repartidores,
    IReadOnlyList<VehiculoRutaDto> Vehiculos,
    IReadOnlyList<EnvioRutaDto> Envios,
    IReadOnlyList<RetiroRutaDto> Retiros,
    IReadOnlyList<DevolucionRutaDto> Devoluciones,
    IReadOnlyList<EntregaRetiroRutaDto> EntregasRetiros,
    IReadOnlyList<FranjaRutaDto> Franjas);

public sealed record HojaDeRutaDto(
    Guid Id,
    DateOnly Fecha,
    TimeOnly? HoraSalida,
    Guid RepartidorId,
    Guid VehiculoId,
    int CantidadParadas,
    decimal PesoAsignadoKg,
    bool Despachada,
    bool Rendida = false,
    bool PendienteDeControl = false);

public sealed class ConsultarHojasDeRutaHandler(
    IRepartidorRepository repartidores,
    IVehiculoRepository vehiculos,
    IEnvioRepository envios,
    IHojaDeRutaRepository hojas,
    IZonaCoberturaRepository zonas,
    IVersionReglasOperativasRepository reglasRepository,
    IRetiroRepository retiros,
    ICuentaComercialRepository cuentas)
{
    public async Task<OpcionesHojaDeRutaDto> ObtenerOpcionesAsync(
        Guid operadorId,
        DateOnly? fecha,
        TimeOnly? horaDesde,
        TimeOnly? horaHasta,
        CancellationToken ct)
    {
        if (operadorId == Guid.Empty)
            throw new ArgumentException(
                "El operador es obligatorio.");

        var listaRepartidores = await repartidores.ListarActivosAsync(
            operadorId, ct);

        var listaVehiculos = await vehiculos.ListarActivosAsync(
            operadorId, ct);

        var listaEnvios = await envios.ListarParaPlanificacionAsync(
            operadorId, ct);

        var listaRetiros = await retiros.ListarParaPlanificacionAsync(
            operadorId,
            ct);

        var listaRetirosParaEntrega = await retiros.ListarParaEntregaComercioAsync(
            operadorId,
            ct);

        var zonasOperador = await zonas.ListarPorOperadorAsync(
            operadorId, ct);

        // Franjas configuradas en las zonas activas del operador.
        var franjas = zonasOperador
            .Where(z => z.Activa)
            .SelectMany(z => z.Franjas)
            .Select(f => new FranjaRutaDto(
                f.HoraDesde,
                f.HoraHasta))
            .Distinct()
            .OrderBy(f => f.HoraDesde)
            .ThenBy(f => f.HoraHasta)
            .ToList();
        var hayFiltros = fecha.HasValue ||
                 horaDesde.HasValue ||
                 horaHasta.HasValue;

        if (hayFiltros && (!fecha.HasValue || !horaDesde.HasValue || !horaHasta.HasValue)){
            throw new ArgumentException(
                "Para filtrar envíos deben indicarse fecha, hora de inicio y hora de fin.");
        }

        if (hayFiltros && horaDesde >= horaHasta){
            throw new ArgumentException(
                "La franja horaria no es válida.");
        }

        DateTimeOffset? inicioEntrega = null;

        if (hayFiltros){
            var zonaHoraria = TimeZoneInfo.FindSystemTimeZoneById(
                "America/Montevideo");

            var fechaHoraLocal = DateTime.SpecifyKind(
                fecha!.Value.ToDateTime(horaDesde!.Value),
                DateTimeKind.Unspecified);

            inicioEntrega = new DateTimeOffset(
                fechaHoraLocal,
                zonaHoraria.GetUtcOffset(fechaHoraLocal));
        }

        var elegibles = new List<Envio>();

        foreach (var envio in listaEnvios)
        {
            // Solo se permiten envíos en depósito sin intentos fallidos
            // o envíos cuya reprogramación ya fue confirmada.
            if (envio.Estado == EstadoEnvio.EnDeposito &&
                envio.IntentosRealizados > 0)
            {
                continue;
            }

            if (envio.Estado != EstadoEnvio.EnDeposito &&
                envio.Estado != EstadoEnvio.Reprogramado)
            {
                continue;
            }

            if (!hayFiltros)
            {
                elegibles.Add(envio);
                continue;
            }

            var zonasCompatibles = zonasOperador
                .Where(z => z.Activa &&
                            z.CubreDireccion(
                                envio.Direccion.Localidad, null))
                .ToList();

            if (zonasCompatibles.Count != 1 || !zonasCompatibles[0].OfreceFranja( horaDesde!.Value, horaHasta!.Value)){
                continue;
            }

            if (envio.Estado == EstadoEnvio.EnDeposito){
                if (envio.IntentosRealizados > 0)
                    continue;

                if (envio.FechaEntregaComprometida.HasValue &&
                    (envio.FechaEntregaComprometida != fecha ||
                    envio.HoraEntregaDesde != horaDesde ||
                    envio.HoraEntregaHasta != horaHasta)){
                    continue;
                }
            }
            else if (envio.Estado == EstadoEnvio.Reprogramado){
                if (envio.VersionReglasOperativasId is not Guid versionId)
                    continue;

                var reglas = await reglasRepository.ObtenerPorIdAsync(
                    operadorId, versionId, ct);

                if (reglas is null ||
                    envio.IntentosRealizados >= reglas.MaximoIntentosEntrega ||
                    envio.UltimoMotivoRequiereDevolucion){
                    continue;
                }

                var ultimoFallo = envio.Intentos
                    .Where(i => !i.Entregado)
                    .OrderByDescending(i => i.OcurridoEn)
                    .FirstOrDefault();

                if (ultimoFallo is null ||
                    inicioEntrega!.Value <
                    ultimoFallo.OcurridoEn.AddHours(
                        reglas.HorasMinimasEntreIntentos)){
                    continue;
                }
            }
            else
            {
                continue;
            }

            elegibles.Add(envio);
        }
        // Envíos disponibles para asignar a una ruta.
        var enviosDisponibles = elegibles
            .Where(x => x.Estado == EstadoEnvio.EnDeposito || x.Estado == EstadoEnvio.Reprogramado)
            .OrderBy(x => x.FechaEntregaComprometida)
            .ThenBy(x => x.HoraEntregaHasta)
            .Select(x => new EnvioRutaDto(
                x.Id,
                x.ReferenciaExterna,
                x.Direccion.Localidad,
                x.Estado.ToString(),
                x.PesoTotalKg,
                x.VolumenTotalMetrosCubicos,
                x.FechaEntregaComprometida,
                x.HoraEntregaDesde,
                x.HoraEntregaHasta,
                x.FechaEntregaComprometida.HasValue &&
                x.HoraEntregaDesde.HasValue &&
                x.HoraEntregaHasta.HasValue))
            .ToList();

        var retirosDisponibles = listaRetiros
            .Where(r =>
                r.Estado == EstadoRetiro.Solicitado &&
                r.HojaDeRutaId == null &&
                (!fecha.HasValue || r.FechaSolicitada == fecha.Value))
            .Where(r =>
            {
                var compatibles = zonasOperador
                    .Where(z =>
                        z.Activa &&
                        string.Equals(
                            z.Departamento,
                            r.DomicilioRetiro.Departamento,
                            StringComparison.OrdinalIgnoreCase) &&
                        z.CubreDireccion(
                            r.DomicilioRetiro.Localidad, null))
                    .ToList();

                return compatibles.Count == 1 &&
                    compatibles[0].OfreceFranja(
                        r.HoraDesde,
                        r.HoraHasta);
            })
            .Select(r => new RetiroRutaDto(
                r.Id,
                r.Motivo,
                r.DomicilioRetiro.Localidad,
                r.Estado.ToString(),
                r.PesoTotalKg,
                r.VolumenTotalMetrosCubicos,
                r.FechaSolicitada,
                r.HoraDesde,
                r.HoraHasta))
            .ToList();

        var entregasRetirosDisponibles = new List<EntregaRetiroRutaDto>();

        foreach (var retiro in listaRetirosParaEntrega)
        {
            var compatibles = zonasOperador
                .Where(z =>
                    z.Activa &&
                    string.Equals(
                        z.Departamento,
                        retiro.DomicilioDestino.Departamento,
                        StringComparison.OrdinalIgnoreCase) &&
                    z.CubreDireccion(
                        retiro.DomicilioDestino.Localidad, null))
                .ToList();

            if (compatibles.Count != 1)
                continue;

            if (hayFiltros &&
                !compatibles[0].OfreceFranja(
                    horaDesde!.Value,
                    horaHasta!.Value))
                continue;

            entregasRetirosDisponibles.Add(
                new EntregaRetiroRutaDto(
                    retiro.Id,
                    retiro.Motivo,
                    retiro.DomicilioDestino.Localidad,
                    retiro.Estado.ToString(),
                    retiro.PesoTotalKg,
                    retiro.VolumenTotalMetrosCubicos));
        }

        var devolucionesDisponibles = new List<DevolucionRutaDto>();
        foreach (var envio in listaEnvios.Where(e =>
            e.Estado == EstadoEnvio.EnDevolucion))
        {
            var hojaActiva = await hojas.ObtenerPorEnvioActivoAsync(
                operadorId,
                envio.Id,
                ct);

            if (hojaActiva is not null)
                continue;

            var cuenta = await cuentas.ObtenerAsync(
                envio.CuentaComercialId,
                ct);

            if (cuenta is null ||
                cuenta.OperadorId != operadorId ||
                !cuenta.Activa ||
                cuenta.Estado != EstadoCuentaComercial.Confirmada ||
                cuenta.DireccionRecepcion is null)
                continue;

            var direccion = cuenta.DireccionRecepcion;

            var compatibles = zonasOperador
                .Where(z =>
                    z.Activa &&
                    string.Equals(
                        z.Departamento,
                        direccion.Departamento,
                        StringComparison.OrdinalIgnoreCase) &&
                    z.CubreDireccion(direccion.Localidad, null))
                .ToList();

            if (compatibles.Count != 1)
                continue;

            if (hayFiltros &&
                !compatibles[0].OfreceFranja(
                    horaDesde!.Value,
                    horaHasta!.Value))
                continue;

            devolucionesDisponibles.Add(
                new DevolucionRutaDto(
                    envio.Id,
                    envio.ReferenciaExterna,
                    direccion.Localidad,
                    envio.Estado.ToString(),
                    envio.PesoTotalKg,
                    envio.VolumenTotalMetrosCubicos));
        }

        return new OpcionesHojaDeRutaDto(
            listaRepartidores
                .Select(x => new RepartidorRutaDto(
                    x.Id,
                    x.Nombre))
                .ToList(),

            listaVehiculos
                .Select(x => new VehiculoRutaDto(
                    x.Id,
                    x.Matricula,
                    x.CapacidadPesoKg,
                    x.CapacidadVolumenM3))
                .ToList(),

            enviosDisponibles,
            retirosDisponibles,
            devolucionesDisponibles,
            entregasRetirosDisponibles,
            franjas);
    }

    public async Task<IReadOnlyList<HojaDeRutaDto>> ListarAsync(
        Guid operadorId,
        CancellationToken ct)
    {
        if (operadorId == Guid.Empty)
            throw new ArgumentException(
                "El operador es obligatorio.");

        var lista = await hojas.ListarAsync(
            operadorId, ct);

        return lista
            .Select(x => new HojaDeRutaDto(
                x.Id,
                x.Fecha,
                x.HoraSalida,
                x.RepartidorId,
                x.VehiculoId,
                x.Paradas.Count(p => p.Activa),
                x.PesoAsignadoKg,
                x.Estado != EstadoHojaDeRuta.EnPreparacion,
                x.Estado == EstadoHojaDeRuta.Rendida,
                x.Estado == EstadoHojaDeRuta.PendienteDeControl))
            .ToList();
    }
}
