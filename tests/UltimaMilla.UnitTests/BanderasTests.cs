using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Abstractions.Caching;
using UltimaMilla.Domain.Plataforma;
using UltimaMilla.Infrastructure.Plataforma;

namespace UltimaMilla.UnitTests;

/// <summary>Aclaración 3.2: banderas por operador, compartidas entre réplicas y auditadas.</summary>
public class BanderasTests
{
    [Fact]
    public async Task Una_bandera_apagada_en_la_cache_compartida_se_respeta_sin_ir_a_la_base()
    {
        var operadorId = Guid.NewGuid();
        var cache = new CacheConBanderas(BanderasService.Clave(operadorId), [Banderas.RetiroDomicilio]);

        // Sin base: si la caché responde, no se consulta.
        var banderas = new BanderasService(null!, cache);

        Assert.False(await banderas.EstaHabilitadaAsync(Banderas.RetiroDomicilio, operadorId, CancellationToken.None));
        Assert.True(await banderas.EstaHabilitadaAsync(Banderas.EntregaParcial, operadorId, CancellationToken.None));
    }

    [Fact]
    public void Cada_cambio_registra_quien_y_cuando()
    {
        var ahora = new DateTimeOffset(2026, 11, 12, 18, 0, 0, TimeSpan.Zero);
        var bandera = BanderaFuncionalidad.Crear(Banderas.EntregaParcial, Guid.NewGuid());

        var cambio = bandera.Cambiar(false, "admin.plataforma@demo.local", ahora);

        Assert.False(bandera.Habilitada);
        Assert.Equal("admin.plataforma@demo.local", cambio.ModificadoPor);
        Assert.Equal(ahora, cambio.ModificadoEn);
        Assert.Throws<ArgumentException>(() => bandera.Cambiar(true, " ", ahora));
    }

    private sealed class CacheConBanderas(string clave, List<string> deshabilitadas) : ICacheService
    {
        public Task<T?> ObtenerAsync<T>(string c, CancellationToken ct = default) =>
            Task.FromResult(c == clave ? (T?)(object)deshabilitadas : default);

        public Task GuardarAsync<T>(string c, T valor, TimeSpan duracion, CancellationToken ct = default) => Task.CompletedTask;
        public Task EliminarAsync(string c, CancellationToken ct = default) => Task.CompletedTask;
        public Task EliminarPorPrefijoAsync(string prefijo, CancellationToken ct = default) => Task.CompletedTask;
    }
}
