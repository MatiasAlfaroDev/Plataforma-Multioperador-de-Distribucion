
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Configuracion.Commands.ActualizarReglasOperativas;
using UltimaMilla.Domain.Configuracion;

namespace UltimaMilla.UnitTests;

public class ActualizarReglasOperativasHandlerTests
{
    private static readonly DateTimeOffset Ahora =
        new(2026, 10, 31, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Sin_version_previa_crea_primera_version()
    {
        var operadorId = Guid.NewGuid();
        var repositorio = new ReglasEnMemoria();
        var unidad = new UnidadDeTrabajoEnMemoria();

        var handler = new ActualizarReglasOperativasHandler(
            repositorio, unidad, new RelojFijo(Ahora));

        var command = new ActualizarReglasOperativasCommand(
            operadorId, 3, 24, true, 7);

        var id = await handler.Handle(command, CancellationToken.None);

        Assert.Single(repositorio.Lista);
        Assert.Equal(id, repositorio.Lista[0].Id);
        Assert.Equal(3, repositorio.Lista[0].MaximoIntentosEntrega);
        Assert.True(repositorio.Lista[0].EstaVigente(Ahora));
        Assert.Equal(1, unidad.Guardados);
    }

    [Fact]
    public async Task Actualizar_cierra_version_anterior_y_crea_otra()
    {
        var operadorId = Guid.NewGuid();

        var anterior = VersionReglasOperativas.Crear(
            operadorId, 3, 24, true, 7,
            Ahora.AddDays(-1));

        var repositorio = new ReglasEnMemoria();
        repositorio.Lista.Add(anterior);

        var unidad = new UnidadDeTrabajoEnMemoria();

        var handler = new ActualizarReglasOperativasHandler(
            repositorio, unidad, new RelojFijo(Ahora));

        var command = new ActualizarReglasOperativasCommand(
            operadorId, 2, 12, false, 5);

        var nuevoId = await handler.Handle(
            command, CancellationToken.None);

        Assert.Equal(2, repositorio.Lista.Count);

        // La versión anterior conserva sus valores.
        Assert.Equal(3, anterior.MaximoIntentosEntrega);
        Assert.Equal(Ahora, anterior.VigenciaHasta);
        Assert.False(anterior.EstaVigente(Ahora));

        // La nueva versión queda vigente.
        var nueva = repositorio.Lista.Single(
            r => r.Id == nuevoId);

        Assert.Equal(2, nueva.MaximoIntentosEntrega);
        Assert.Equal(12, nueva.HorasMinimasEntreIntentos);
        Assert.False(nueva.RequierePruebaEntrega);
        Assert.Equal(5, nueva.DiasMaximosParaDevolucion);
        Assert.True(nueva.EstaVigente(Ahora));

        Assert.Equal(1, unidad.Guardados);
    }

    private sealed class ReglasEnMemoria
        : IVersionReglasOperativasRepository
    {
        public List<VersionReglasOperativas> Lista { get; } = [];

        public Task<VersionReglasOperativas?> ObtenerVigenteAsync(
            Guid operadorId,
            DateTimeOffset fecha,
            CancellationToken ct) =>
            ObtenerVigenteParaActualizarAsync(
                operadorId, fecha, ct);

        public Task<VersionReglasOperativas?> ObtenerVigenteParaActualizarAsync(
            Guid operadorId,
            DateTimeOffset fecha,
            CancellationToken ct) =>
            Task.FromResult(Lista
                .Where(r => r.OperadorId == operadorId &&
                            r.EstaVigente(fecha))
                .OrderByDescending(r => r.VigenciaDesde)
                .FirstOrDefault());

        public Task<VersionReglasOperativas?> ObtenerPorIdAsync(
            Guid operadorId,
            Guid versionId,
            CancellationToken ct) =>
            Task.FromResult(Lista.FirstOrDefault(r =>
                r.OperadorId == operadorId &&
                r.Id == versionId));

        public Task AgregarAsync(
            VersionReglasOperativas version,
            CancellationToken ct)
        {
            Lista.Add(version);
            return Task.CompletedTask;
        }
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
            CancellationToken ct) =>
            operacion(ct);
    }

    private sealed class RelojFijo(DateTimeOffset ahora)
        : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => ahora;
    }
}
