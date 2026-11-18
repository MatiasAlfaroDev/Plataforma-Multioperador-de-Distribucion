using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Reportes.Commands.CerrarDia;
using UltimaMilla.Domain.Envios;
using UltimaMilla.Domain.Reportes;

namespace UltimaMilla.UnitTests;

/// <summary>Letra 4: cierre diario de la operación que corre el Worker.</summary>
public class CierreDiarioTests
{
    private static readonly Guid OperadorA = Guid.NewGuid();
    private static readonly Guid OperadorB = Guid.NewGuid();
    private static readonly DateOnly Fecha = new(2026, 11, 16);
    private static readonly DateTimeOffset Ahora = new(2026, 11, 17, 5, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Calcula_los_indicadores_del_dia_en_hora_de_Uruguay_y_los_guarda()
    {
        var reportes = new ReportesFalsos();
        reportes.Fijar(OperadorA, enviados: 7, completas: 3, parciales: 1, fallidas: 1, aTiempo: 2, fueraDePlazo: 1);
        var cierres = new CierresEnMemoria();
        var unitOfWork = new UnitOfWorkFalso();

        var cierre = await Handler(reportes, cierres.Del(OperadorA), unitOfWork, Ahora)
            .Handle(new CerrarDiaCommand(OperadorA, Fecha), default);

        Assert.Equal(7, cierre.EnviosCreados);
        Assert.Equal(3, cierre.EntregasCompletas);
        Assert.Equal(1, cierre.EntregasParciales);
        Assert.Equal(1, cierre.IntentosFallidos);
        Assert.Equal(60m, cierre.PorcentajeCumplimiento);
        Assert.Equal(2, cierre.ATiempo);
        Assert.Equal(1, cierre.FueraDePlazo);
        Assert.Equal(Ahora, cierre.CalculadoEn);
        Assert.Single(cierres.Todos);
        Assert.Equal(1, unitOfWork.Guardados);

        // El día de Uruguay (UTC-3) va de las 03:00 UTC a las 03:00 UTC del día siguiente.
        Assert.Equal(new DateTimeOffset(2026, 11, 16, 3, 0, 0, TimeSpan.Zero), reportes.Desde);
        Assert.Equal(new DateTimeOffset(2026, 11, 17, 3, 0, 0, TimeSpan.Zero), reportes.Hasta);
    }

    [Fact]
    public async Task Volver_a_cerrar_el_mismo_dia_recalcula_sin_duplicar()
    {
        var reportes = new ReportesFalsos();
        var cierres = new CierresEnMemoria();

        reportes.Fijar(OperadorA, enviados: 5, completas: 2, parciales: 0, fallidas: 0, aTiempo: 2, fueraDePlazo: 0);
        await Handler(reportes, cierres.Del(OperadorA), new UnitOfWorkFalso(), Ahora)
            .Handle(new CerrarDiaCommand(OperadorA, Fecha), default);

        // Llegaron eventos tarde de la app sin conexión.
        reportes.Fijar(OperadorA, enviados: 5, completas: 3, parciales: 0, fallidas: 1, aTiempo: 2, fueraDePlazo: 1);
        var despues = Ahora.AddHours(1);
        await Handler(reportes, cierres.Del(OperadorA), new UnitOfWorkFalso(), despues)
            .Handle(new CerrarDiaCommand(OperadorA, Fecha), default);

        var cierre = Assert.Single(cierres.Todos);
        Assert.Equal(3, cierre.EntregasCompletas);
        Assert.Equal(1, cierre.IntentosFallidos);
        Assert.Equal(75m, cierre.PorcentajeCumplimiento);
        Assert.Equal(despues, cierre.CalculadoEn);
    }

    [Fact]
    public async Task Cada_operador_tiene_su_propio_cierre()
    {
        var reportes = new ReportesFalsos();
        reportes.Fijar(OperadorA, enviados: 4, completas: 4, parciales: 0, fallidas: 0, aTiempo: 4, fueraDePlazo: 0);
        reportes.Fijar(OperadorB, enviados: 9, completas: 1, parciales: 0, fallidas: 3, aTiempo: 0, fueraDePlazo: 1);
        var cierres = new CierresEnMemoria();

        await Handler(reportes, cierres.Del(OperadorA), new UnitOfWorkFalso(), Ahora)
            .Handle(new CerrarDiaCommand(OperadorA, Fecha), default);
        await Handler(reportes, cierres.Del(OperadorB), new UnitOfWorkFalso(), Ahora)
            .Handle(new CerrarDiaCommand(OperadorB, Fecha), default);

        Assert.Equal(2, cierres.Todos.Count);
        Assert.Equal(4, cierres.Todos.Single(c => c.OperadorId == OperadorA).EnviosCreados);
        Assert.Equal(9, cierres.Todos.Single(c => c.OperadorId == OperadorB).EnviosCreados);
        Assert.Equal(25m, cierres.Todos.Single(c => c.OperadorId == OperadorB).PorcentajeCumplimiento);
    }

    [Fact]
    public async Task Un_dia_sin_entregas_no_tiene_porcentaje_de_cumplimiento()
    {
        var cierre = await Handler(new ReportesFalsos(), new CierresEnMemoria().Del(OperadorA), new UnitOfWorkFalso(), Ahora)
            .Handle(new CerrarDiaCommand(OperadorA, Fecha), default);

        Assert.Equal(0, cierre.EnviosCreados);
        Assert.Null(cierre.PorcentajeCumplimiento);
    }

    private static CerrarDiaHandler Handler(
        IReporteEnviosRepository reportes, ICierreDiarioRepository cierres, IUnitOfWork unitOfWork, DateTimeOffset ahora) =>
        new(reportes, cierres, unitOfWork, new RelojFijo(ahora));

    private sealed class ReportesFalsos : IReporteEnviosRepository
    {
        private readonly Dictionary<Guid, (int Enviados, CumplimientoZonaItem Zona, ResultadoPuntualidadReporte Puntualidad)> datos = [];

        public DateTimeOffset Desde { get; private set; }
        public DateTimeOffset Hasta { get; private set; }

        public void Fijar(Guid operadorId, int enviados, int completas, int parciales, int fallidas, int aTiempo, int fueraDePlazo) =>
            datos[operadorId] = (
                enviados,
                new CumplimientoZonaItem(null, "Zona sin determinar", completas, parciales, fallidas, 0),
                new ResultadoPuntualidadReporte(aTiempo, fueraDePlazo, 0, parciales, null));

        public Task<IReadOnlyList<ConteoEnviosPorEstado>> ContarPorEstadoAsync(Guid operadorId, DateTimeOffset desde, DateTimeOffset hastaExclusivo, CancellationToken ct)
        {
            (Desde, Hasta) = (desde, hastaExclusivo);
            IReadOnlyList<ConteoEnviosPorEstado> conteos = datos.TryGetValue(operadorId, out var d) && d.Enviados > 0
                ? [new(EstadoEnvio.Admitido, d.Enviados - 1), new(EstadoEnvio.Entregado, 1)]
                : [];
            return Task.FromResult(conteos);
        }

        public Task<CumplimientoReporte> ObtenerCumplimientoAsync(Guid operadorId, DateTimeOffset desde, DateTimeOffset hastaExclusivo, CancellationToken ct) =>
            Task.FromResult(new CumplimientoReporte(datos.TryGetValue(operadorId, out var d) ? [d.Zona] : [], []));

        public Task<ResultadoPuntualidadReporte> ObtenerPuntualidadAsync(Guid operadorId, DateTimeOffset desde, DateTimeOffset hastaExclusivo, CancellationToken ct) =>
            Task.FromResult(datos.TryGetValue(operadorId, out var d) ? d.Puntualidad : new ResultadoPuntualidadReporte(0, 0, 0, 0, null));

        public Task<IReadOnlyList<ConteoMotivoNoEntrega>> ContarMotivosNoEntregaAsync(Guid operadorId, DateTimeOffset desde, DateTimeOffset hastaExclusivo, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<VolumenDiarioReporte>> ObtenerVolumenDiarioAsync(Guid operadorId, DateTimeOffset desde, DateTimeOffset hastaExclusivo, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<ResumenComercioReporte>> ObtenerPorComercioAsync(Guid operadorId, DateTimeOffset desde, DateTimeOffset hastaExclusivo, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<ResultadoRepartidorReporte>> ObtenerResultadosPorRepartidorAsync(Guid operadorId, DateTimeOffset desde, DateTimeOffset hastaExclusivo, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<DetalleLiquidacionComercio>> ObtenerDetalleLiquidacionAsync(Guid operadorId, DateTimeOffset desde, DateTimeOffset hastaExclusivo, CancellationToken ct) => throw new NotSupportedException();
    }

    /// <summary>Una tabla compartida; cada repositorio ve solo lo de su operador, como el filtro global de AppDbContext.</summary>
    private sealed class CierresEnMemoria
    {
        public List<CierreDiario> Todos { get; } = [];

        public ICierreDiarioRepository Del(Guid operadorId) => new Repositorio(this, operadorId);

        private sealed class Repositorio(CierresEnMemoria tabla, Guid operadorId) : ICierreDiarioRepository
        {
            private IEnumerable<CierreDiario> Visibles => tabla.Todos.Where(c => c.OperadorId == operadorId);

            public Task<CierreDiario?> ObtenerAsync(DateOnly fecha, CancellationToken ct) =>
                Task.FromResult(Visibles.SingleOrDefault(c => c.Fecha == fecha));

            public Task<IReadOnlyList<CierreDiario>> ListarAsync(DateOnly desde, DateOnly hasta, CancellationToken ct) =>
                Task.FromResult<IReadOnlyList<CierreDiario>>(Visibles.Where(c => c.Fecha >= desde && c.Fecha <= hasta).ToList());

            public Task AgregarAsync(CierreDiario cierre, CancellationToken ct)
            {
                if (cierre.OperadorId != operadorId)
                    throw new InvalidOperationException("Cierre de otro operador.");
                tabla.Todos.Add(cierre);
                return Task.CompletedTask;
            }
        }
    }

    private sealed class UnitOfWorkFalso : IUnitOfWork
    {
        public int Guardados { get; private set; }

        public Task<int> GuardarCambiosAsync(CancellationToken ct)
        {
            Guardados++;
            return Task.FromResult(1);
        }

        public Task EjecutarEnTransaccionAsync(Func<CancellationToken, Task> operacion, CancellationToken ct) => operacion(ct);
    }

    private sealed class RelojFijo(DateTimeOffset ahora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => ahora;
    }
}
