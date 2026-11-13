using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Tablero.Queries.ObtenerTableroEnVivo;
using UltimaMilla.Domain.Envios;

namespace UltimaMilla.UnitTests;

/// <summary>RF 29: envíos en riesgo de incumplir su ventana comprometida.</summary>
public class TableroEnVivoTests
{
    // 13/11/2026 15:00 en Montevideo (UTC-3).
    private static readonly DateTimeOffset Ahora = new(2026, 11, 13, 18, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Hoy = new(2026, 11, 13);

    [Fact]
    public async Task Marca_en_riesgo_lo_que_cierra_dentro_del_margen_y_lo_vencido_ordenado_por_urgencia()
    {
        var repositorio = new TableroFalso(
            Envio("cierra-en-30", Hoy, new(14, 0), new(15, 30)),
            Envio("cierra-en-3h", Hoy, new(16, 0), new(18, 0)),
            Envio("vencido-ayer", Hoy.AddDays(-1), new(9, 0), new(12, 0)),
            Envio("cierra-en-60", Hoy, new(15, 0), new(16, 0)));

        var tablero = await new ObtenerTableroEnVivoHandler(repositorio, new RelojFijo(Ahora)).Handle(default);

        Assert.Equal(["vencido-ayer", "cierra-en-30", "cierra-en-60"], tablero.EnRiesgo.Select(e => e.Referencia));
        Assert.True(tablero.EnRiesgo[0].Vencido);
        Assert.Equal(30, tablero.EnRiesgo[1].MinutosRestantes);
        Assert.False(tablero.EnRiesgo[1].Vencido);
        Assert.Equal(new DateOnly(2026, 11, 13), repositorio.HastaPedido);
    }

    private static EnvioConVentana Envio(string referencia, DateOnly fecha, TimeOnly desde, TimeOnly hasta) =>
        new(Guid.NewGuid(), referencia, EstadoEnvio.EnTransito, "Ana", "Calle 1, Centro, Montevideo", fecha, desde, hasta);

    private sealed class TableroFalso(params EnvioConVentana[] pendientes) : ITableroRepository
    {
        public DateOnly? HastaPedido { get; private set; }

        public Task<IReadOnlyList<ConteoEstado>> ContarPorEstadoAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<ConteoEstado>>([]);

        public Task<IReadOnlyList<PosicionFlotaDto>> UltimasPosicionesAsync(DateTimeOffset desde, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<PosicionFlotaDto>>([]);

        public Task<IReadOnlyList<EnvioConVentana>> PendientesConVentanaHastaAsync(DateOnly hasta, CancellationToken ct)
        {
            HastaPedido = hasta;
            return Task.FromResult<IReadOnlyList<EnvioConVentana>>(pendientes);
        }
    }

    private sealed class RelojFijo(DateTimeOffset ahora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => ahora;
    }
}
