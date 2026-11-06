
using Microsoft.EntityFrameworkCore;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Domain.Configuracion;
using UltimaMilla.Infrastructure.Persistence;
using UltimaMilla.Domain.Envios;

namespace UltimaMilla.Infrastructure.Repositories;

public sealed class ReporteEnviosRepository(
    AppDbContext db) : IReporteEnviosRepository
{
    public async Task<IReadOnlyList<ConteoEnviosPorEstado>>
        ContarPorEstadoAsync(
            Guid operadorId,
            DateTimeOffset desde,
            DateTimeOffset hastaExclusivo,
            CancellationToken ct)
    {
        if (operadorId == Guid.Empty)
            throw new ArgumentException("El operador es obligatorio.");

        return await db.Envios
            .AsNoTracking()
            .Where(e =>
                e.OperadorId == operadorId &&
                e.FechaAlta >= desde &&
                e.FechaAlta < hastaExclusivo)
            .GroupBy(e => e.Estado)
            .Select(g => new ConteoEnviosPorEstado(
                g.Key,
                g.Count()))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ConteoMotivoNoEntrega>>
        ContarMotivosNoEntregaAsync(
            Guid operadorId,
            DateTimeOffset desde,
            DateTimeOffset hastaExclusivo,
            CancellationToken ct)
    {
        if (operadorId == Guid.Empty)
            throw new ArgumentException("El operador es obligatorio.");

        // Intentos de entrega completamente fallidos.
        var intentos = await (
            from intento in db.IntentosEntrega.AsNoTracking()
            join motivo in db.Set<MotivoNoEntrega>().AsNoTracking()
                on intento.MotivoNoEntregaId equals (Guid?)motivo.Id
            where intento.OperadorId == operadorId
                && motivo.OperadorId == operadorId
                && intento.OcurridoEn >= desde
                && intento.OcurridoEn < hastaExclusivo
                && !intento.Entregado
            group intento by new
            {
                motivo.Codigo,
                motivo.Descripcion
            }
            into grupo
            select new
            {
                grupo.Key.Codigo,
                grupo.Key.Descripcion,
                Cantidad = grupo.Count()
            }
        ).ToListAsync(ct);

        // Bultos que no se entregaron en entregas parciales.
        var bultos = await (
            from resultado in db.ResultadosBultosIntento.AsNoTracking()
            join intento in db.IntentosEntrega.AsNoTracking()
                on resultado.IntentoEntregaId equals intento.Id
            join motivo in db.Set<MotivoNoEntrega>().AsNoTracking()
                on resultado.MotivoNoEntregaId equals (Guid?)motivo.Id
            where intento.OperadorId == operadorId
                && motivo.OperadorId == operadorId
                && intento.OcurridoEn >= desde
                && intento.OcurridoEn < hastaExclusivo
                && !resultado.Entregado
            group resultado by new
            {
                motivo.Codigo,
                motivo.Descripcion
            }
            into grupo
            select new
            {
                grupo.Key.Codigo,
                grupo.Key.Descripcion,
                Cantidad = grupo.Count()
            }
        ).ToListAsync(ct);

        // Unificamos los motivos de diferentes versiones.
        var resultados = intentos
            .Select(x => new ConteoMotivoNoEntrega(
                x.Codigo, x.Descripcion, x.Cantidad, 0))
            .Concat(bultos.Select(x => new ConteoMotivoNoEntrega(
                x.Codigo, x.Descripcion, 0, x.Cantidad)))
            .GroupBy(x => x.Codigo)
            .Select(g => new ConteoMotivoNoEntrega(
                g.Key,
                g.OrderBy(x => x.Descripcion)
                    .First().Descripcion,
                g.Sum(x => x.IntentosFallidos),
                g.Sum(x => x.BultosNoEntregados)))
            .OrderByDescending(x =>
                x.IntentosFallidos + x.BultosNoEntregados)
            .ThenBy(x => x.Codigo)
            .ToList();

        return resultados;
    }

    
    public async Task<IReadOnlyList<VolumenDiarioReporte>>
        ObtenerVolumenDiarioAsync(
            Guid operadorId,
            DateTimeOffset desde,
            DateTimeOffset hastaExclusivo,
            CancellationToken ct)
    {
        if (operadorId == Guid.Empty)
            throw new ArgumentException("El operador es obligatorio.");

        var datos = await db.Envios
            .AsNoTracking()
            .Where(e =>
                e.OperadorId == operadorId &&
                e.FechaAlta >= desde &&
                e.FechaAlta < hastaExclusivo)
            .GroupBy(e => e.FechaAlta.Date)
            .Select(g => new
            {
                Fecha = g.Key,
                Cantidad = g.Count()
            })
            .OrderBy(x => x.Fecha)
            .ToListAsync(ct);

        return datos
            .Select(x => new VolumenDiarioReporte(
                DateOnly.FromDateTime(x.Fecha),
                x.Cantidad))
            .ToList();
    }

    public async Task<IReadOnlyList<ResumenComercioReporte>>
        ObtenerPorComercioAsync(
            Guid operadorId,
            DateTimeOffset desde,
            DateTimeOffset hastaExclusivo,
            CancellationToken ct)
    {
        if (operadorId == Guid.Empty)
            throw new ArgumentException("El operador es obligatorio.");

        return await (
            from envio in db.Envios.AsNoTracking()
            join cuenta in db.CuentasComerciales.AsNoTracking()
                on envio.CuentaComercialId equals cuenta.Id
            join comercio in db.Comercios.AsNoTracking()
                on cuenta.ComercioId equals comercio.Id
            where envio.OperadorId == operadorId
                && cuenta.OperadorId == operadorId
                && envio.FechaAlta >= desde
                && envio.FechaAlta < hastaExclusivo
            group envio by new
            {
                CuentaId = cuenta.Id,
                Comercio = comercio.Nombre
            }
            into grupo
            orderby grupo.Key.Comercio
            select new ResumenComercioReporte(
                grupo.Key.CuentaId,
                grupo.Key.Comercio,
                grupo.Count(),
                grupo.Count(e =>
                    e.Estado == EstadoEnvio.Entregado),
                grupo.Count(e =>
                    e.Estado == EstadoEnvio.EntregadoParcialmente),
                grupo.Sum(e => e.TarifaCalculada ?? 0m))
        ).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ResultadoRepartidorReporte>>
        ObtenerResultadosPorRepartidorAsync(
            Guid operadorId,
            DateTimeOffset desde,
            DateTimeOffset hastaExclusivo,
            CancellationToken ct)
    {
        if (operadorId == Guid.Empty)
            throw new ArgumentException("El operador es obligatorio.");

        // Eventos que representan resultados reales de entrega.
        var eventos = await db.EventosEnvio
            .AsNoTracking()
            .Where(e =>
                e.OperadorId == operadorId &&
                e.OcurridoEn >= desde &&
                e.OcurridoEn < hastaExclusivo &&
                (e.Disparador == DisparadorEnvio.Entregar ||
                e.Disparador == DisparadorEnvio.RegistrarEntregaParcial ||
                e.Disparador == DisparadorEnvio.RegistrarIntentoFallido))
            .Select(e => new
            {
                e.EnvioId,
                e.OcurridoEn,
                e.Disparador
            })
            .ToListAsync(ct);

        if (eventos.Count == 0)
            return [];

        var envioIds = eventos
            .Select(e => e.EnvioId)
            .Distinct()
            .ToArray();

        // Incluye paradas históricas, no solamente las activas.
        var asignaciones = await db.HojasDeRuta
            .AsNoTracking()
            .Where(h =>
                h.OperadorId == operadorId &&
                h.DespachadaEn != null)
            .SelectMany(h => h.Paradas.Select(p => new
            {
                p.EnvioId,
                h.RepartidorId,
                h.DespachadaEn,
                p.LiberadaEn
            }))
            .Where(x => envioIds.Contains(x.EnvioId))
            .ToListAsync(ct);

        var repartidores = await db.Repartidores
            .AsNoTracking()
            .Where(r => r.OperadorId == operadorId)
            .Select(r => new { r.Id, r.Nombre })
            .ToDictionaryAsync(r => r.Id, r => r.Nombre, ct);

        var resultados = eventos.Select(e =>
        {
            // La ruta debe haber salido antes del evento y la
            // parada no puede haberse liberado antes de él.
            var candidatas = asignaciones
                .Where(a =>
                    a.EnvioId == e.EnvioId &&
                    a.DespachadaEn <= e.OcurridoEn &&
                    (a.LiberadaEn == null ||
                    a.LiberadaEn >= e.OcurridoEn))
                .OrderByDescending(a => a.DespachadaEn)
                .ToList();

            Guid? repartidorId = null;

            if (candidatas.Count > 0)
            {
                var ultimaSalida = candidatas[0].DespachadaEn;

                var posibles = candidatas
                    .Where(a => a.DespachadaEn == ultimaSalida)
                    .Select(a => a.RepartidorId)
                    .Distinct()
                    .ToList();

                // Una atribución ambigua se informa como
                // no determinada, en vez de inventarla.
                if (posibles.Count == 1)
                    repartidorId = posibles[0];
            }

            return new
            {
                RepartidorId = repartidorId,
                e.Disparador
            };
        });

        return resultados
            .GroupBy(x => x.RepartidorId)
            .Select(g => new ResultadoRepartidorReporte(
                g.Key,
                g.Key.HasValue &&
                    repartidores.TryGetValue(g.Key.Value, out var nombre)
                    ? nombre
                    : "Sin repartidor determinado",
                g.Count(x => x.Disparador == DisparadorEnvio.Entregar),
                g.Count(x => x.Disparador ==
                    DisparadorEnvio.RegistrarEntregaParcial),
                g.Count(x => x.Disparador ==
                    DisparadorEnvio.RegistrarIntentoFallido)))
            .OrderBy(x => x.Repartidor)
            .ToList();
    }
    public async Task<CumplimientoReporte> ObtenerCumplimientoAsync(
        Guid operadorId,
        DateTimeOffset desde,
        DateTimeOffset hastaExclusivo,
        CancellationToken ct)
    {
        if (operadorId == Guid.Empty)
            throw new ArgumentException("El operador es obligatorio.");

        // Cada fila representa un resultado de entrega registrado,
        // no necesariamente un envío único.
        var datos = await (
            from evento in db.EventosEnvio.AsNoTracking()
            join envio in db.Envios.AsNoTracking()
                on evento.EnvioId equals envio.Id
            join cuenta in db.CuentasComerciales.AsNoTracking()
                on envio.CuentaComercialId equals cuenta.Id
            join comercio in db.Comercios.AsNoTracking()
                on cuenta.ComercioId equals comercio.Id
            where evento.OperadorId == operadorId
                && envio.OperadorId == operadorId
                && cuenta.OperadorId == operadorId
                && evento.OcurridoEn >= desde
                && evento.OcurridoEn < hastaExclusivo
                && (
                    evento.Disparador == DisparadorEnvio.Entregar ||
                    evento.Disparador == DisparadorEnvio.RegistrarEntregaParcial ||
                    evento.Disparador == DisparadorEnvio.RegistrarIntentoFallido
                )
            select new
            {
                evento.Disparador,
                CuentaId = cuenta.Id,
                Comercio = comercio.Nombre,
                Localidad = envio.Direccion.Localidad,
                Departamento = envio.Direccion.Departamento
            }
        ).ToListAsync(ct);

        var zonas = await db.ZonasCobertura
            .AsNoTracking()
            .Where(z => z.OperadorId == operadorId)
            .Include(z => z.Localidades)
            .ToListAsync(ct);

        // Solo se identifica una zona cuando existe una única
        // cobertura completa de la localidad y departamento.
        // No se infieren sectores que el envío no registra.
        var resultados = datos.Select(d =>
        {
            var coincidencias = zonas
                .Where(z =>
                    string.Equals(
                        z.Departamento,
                        d.Departamento,
                        StringComparison.OrdinalIgnoreCase)
                    && z.Localidades.Any(l =>
                        l.Sector == null &&
                        string.Equals(
                            l.Nombre,
                            d.Localidad,
                            StringComparison.OrdinalIgnoreCase)))
                .ToList();

            var zona = coincidencias.Count == 1
                ? coincidencias[0]
                : null;

            return new
            {
                d.Disparador,
                d.CuentaId,
                d.Comercio,
                ZonaId = zona?.Id,
                ZonaNombre = zona?.Nombre ?? "Zona sin determinar"
            };
        }).ToList();

        var porZona = resultados
            .GroupBy(x => new { x.ZonaId, x.ZonaNombre })
            .Select(g =>
            {
                var completas = g.Count(x =>
                    x.Disparador == DisparadorEnvio.Entregar);

                var parciales = g.Count(x =>
                    x.Disparador == DisparadorEnvio.RegistrarEntregaParcial);

                var fallidas = g.Count(x =>
                    x.Disparador == DisparadorEnvio.RegistrarIntentoFallido);

                var total = completas + parciales + fallidas;

                return new CumplimientoZonaItem(
                    g.Key.ZonaId,
                    g.Key.ZonaNombre,
                    completas,
                    parciales,
                    fallidas,
                    total == 0 ? 0 :
                        Math.Round(100m * completas / total, 2));
            })
            .OrderBy(x => x.Zona)
            .ToList();

        var porComercio = resultados
            .GroupBy(x => new { x.CuentaId, x.Comercio })
            .Select(g =>
            {
                var completas = g.Count(x =>
                    x.Disparador == DisparadorEnvio.Entregar);

                var parciales = g.Count(x =>
                    x.Disparador == DisparadorEnvio.RegistrarEntregaParcial);

                var fallidas = g.Count(x =>
                    x.Disparador == DisparadorEnvio.RegistrarIntentoFallido);

                var total = completas + parciales + fallidas;

                return new CumplimientoComercioItem(
                    g.Key.CuentaId,
                    g.Key.Comercio,
                    completas,
                    parciales,
                    fallidas,
                    total == 0 ? 0 :
                        Math.Round(100m * completas / total, 2));
            })
            .OrderBy(x => x.Comercio)
            .ToList();

        return new CumplimientoReporte(porZona, porComercio);
    }
    
    public async Task<IReadOnlyList<DetalleLiquidacionComercio>>
        ObtenerDetalleLiquidacionAsync(
            Guid operadorId,
            DateTimeOffset desde,
            DateTimeOffset hastaExclusivo,
            CancellationToken ct)
    {
        if (operadorId == Guid.Empty)
            throw new ArgumentException("El operador es obligatorio.");

        var datos = await (
            from envio in db.Envios.AsNoTracking()
            join cuenta in db.CuentasComerciales.AsNoTracking()
                on envio.CuentaComercialId equals cuenta.Id
            join comercio in db.Comercios.AsNoTracking()
                on cuenta.ComercioId equals comercio.Id
            where envio.OperadorId == operadorId
                && cuenta.OperadorId == operadorId
                && envio.FechaAlta >= desde
                && envio.FechaAlta < hastaExclusivo
            orderby comercio.Nombre, envio.FechaAlta
            select new
            {
                EnvioId = envio.Id,
                envio.ReferenciaExterna,
                envio.FechaAlta,
                envio.Estado,
                envio.TarifaCalculada,
                CuentaId = cuenta.Id,
                Comercio = comercio.Nombre
            }
        ).ToListAsync(ct);

        return datos
            .GroupBy(x => new { x.CuentaId, x.Comercio })
            .Select(grupo => new DetalleLiquidacionComercio(
                grupo.Key.CuentaId,
                grupo.Key.Comercio,
                grupo.Count(),
                grupo.Count(x => !x.TarifaCalculada.HasValue),
                grupo.Sum(x => x.TarifaCalculada ?? 0m),
                grupo.Select(x => new DetalleLiquidacionEnvio(
                    x.EnvioId,
                    x.ReferenciaExterna ?? string.Empty,
                    x.FechaAlta,
                    x.Estado.ToString(),
                    x.TarifaCalculada
                )).ToList()
            ))
            .OrderBy(x => x.Comercio)
            .ToList();
    }

}
