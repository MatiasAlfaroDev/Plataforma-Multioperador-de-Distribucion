using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Common;
using UltimaMilla.Application.Configuracion.Commands.CrearZona;
using UltimaMilla.Application.Configuracion.Commands.EditarZona;
using UltimaMilla.Domain.Configuracion;

namespace UltimaMilla.UnitTests;

public class EditarZonaHandlerTests
{
    private static readonly Guid Operador = Guid.NewGuid();

    private readonly ZonasEnMemoria _zonas = new();
    private readonly UnidadDeTrabajoEnMemoria _unidad = new();
    private readonly EditarZonaHandler _handler;
    private readonly ZonaCobertura _centro;
    private readonly ZonaCobertura _norte;

    public EditarZonaHandlerTests()
    {
        _centro = Zona("Centro", "San José de Mayo");
        _norte = Zona("Norte", "Ciudad del Plata");
        _zonas.Lista.AddRange([_centro, _norte]);
        _handler = new EditarZonaHandler(_zonas, _unidad);
    }

    private static ZonaCobertura Zona(string nombre, string localidad)
    {
        var zona = ZonaCobertura.Crear(Operador, nombre, "San José");
        zona.AgregarLocalidad(localidad);
        zona.AgregarFranja(new TimeOnly(9, 0), new TimeOnly(12, 0));
        return zona;
    }

    private static EditarZonaCommand Editar(Guid zonaId, string nombre, string localidad) =>
        new(Operador, zonaId, nombre, "San José", 20,
            [new CoberturaCommand(localidad, null)],
            [new FranjaEntregaCommand(new TimeOnly(14, 0), new TimeOnly(18, 0))]);

    [Fact]
    public async Task Edicion_valida_cambia_la_zona_y_guarda()
    {
        await _handler.Handle(Editar(_centro.Id, "Centro ampliado", "Libertad"), CancellationToken.None);

        Assert.Equal("Centro ampliado", _centro.Nombre);
        Assert.True(_centro.CubreDireccion("Libertad", null));
        Assert.False(_centro.CubreDireccion("San José de Mayo", null));
        Assert.Equal(20, _centro.MinutosTrasladoEstimado);
        Assert.Equal(1, _unidad.Guardados);
    }

    [Fact]
    public async Task Zona_inexistente_da_no_encontrado()
    {
        await Assert.ThrowsAsync<NoEncontradoException>(() =>
            _handler.Handle(Editar(Guid.NewGuid(), "Otra", "Libertad"), CancellationToken.None));
    }

    [Fact]
    public async Task Nombre_de_otra_zona_se_rechaza()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.Handle(Editar(_centro.Id, "Norte", "Libertad"), CancellationToken.None));
        Assert.Equal(0, _unidad.Guardados);
    }

    [Fact]
    public async Task Cubrir_una_localidad_de_otra_zona_se_rechaza()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.Handle(Editar(_centro.Id, "Centro", "Ciudad del Plata"), CancellationToken.None));

        Assert.Contains("se superpone con la zona 'Norte'", ex.Message);
        Assert.Equal(0, _unidad.Guardados);
    }

    // ----- Dobles de prueba -----

    private sealed class ZonasEnMemoria : IZonaCoberturaRepository
    {
        public List<ZonaCobertura> Lista { get; } = [];

        public Task<IReadOnlyList<ZonaCobertura>> ListarPorOperadorAsync(Guid operadorId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<ZonaCobertura>>(Lista.Where(z => z.OperadorId == operadorId).ToList());

        public Task<bool> ExisteNombreAsync(Guid operadorId, string nombre, CancellationToken ct) =>
            Task.FromResult(Lista.Any(z => z.OperadorId == operadorId &&
                string.Equals(z.Nombre, nombre, StringComparison.OrdinalIgnoreCase)));

        public Task AgregarAsync(ZonaCobertura zona, CancellationToken ct)
        {
            Lista.Add(zona);
            return Task.CompletedTask;
        }

        public Task<ZonaCobertura?> ObtenerAsync(Guid operadorId, Guid zonaId, CancellationToken ct) =>
            Task.FromResult(Lista.FirstOrDefault(z => z.OperadorId == operadorId && z.Id == zonaId));

        public Task<bool> ExisteNombreEnOtraZonaAsync(Guid operadorId, string nombre, Guid zonaId, CancellationToken ct) =>
            Task.FromResult(Lista.Any(z => z.OperadorId == operadorId && z.Id != zonaId &&
                string.Equals(z.Nombre, nombre, StringComparison.OrdinalIgnoreCase)));
    }

    private sealed class UnidadDeTrabajoEnMemoria : IUnitOfWork
    {
        public int Guardados { get; private set; }

        public Task<int> GuardarCambiosAsync(CancellationToken ct)
        {
            Guardados++;
            return Task.FromResult(1);
        }
        public Task EjecutarEnTransaccionAsync(
            Func<CancellationToken, Task> operacion,
            CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(operacion);
            return operacion(ct);
        }
    }
}
