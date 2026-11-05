
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

        var motivos = new MotivosEnMemoria();

        var handler = new ActualizarReglasOperativasHandler(
            repositorio, motivos, unidad, new RelojFijo(Ahora));

        var command = new ActualizarReglasOperativasCommand(
            operadorId,
            3,
            24,
            true,
            7,
            [
                new MotivoNoEntregaItem(
                    "AUSENTE",
                    "Destinatario ausente",
                    false),
                new MotivoNoEntregaItem(
                    "RECHAZADO",
                    "Entrega rechazada",
                    true)
            ]);

        var id = await handler.Handle(command, CancellationToken.None);

        Assert.Single(repositorio.Lista);
        Assert.Equal(id, repositorio.Lista[0].Id);
        Assert.Equal(3, repositorio.Lista[0].MaximoIntentosEntrega);
        Assert.True(repositorio.Lista[0].EstaVigente(Ahora));
        Assert.Equal(1, unidad.Guardados);
        var motivosGuardados = await motivos.ListarPorVersionAsync(
            operadorId,
            id,
            CancellationToken.None);

        Assert.Equal(2, motivosGuardados.Count);
        Assert.Contains(motivosGuardados, m => m.Codigo == "AUSENTE");
        Assert.Contains(motivosGuardados, m => m.Codigo == "RECHAZADO");
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

       var motivos = new MotivosEnMemoria();

        await motivos.AgregarRangoAsync(
            [
                MotivoNoEntrega.Crear(
                    operadorId,
                    anterior.Id,
                    "AUSENTE",
                    "Destinatario ausente",
                    false),
                MotivoNoEntrega.Crear(
                    operadorId,
                    anterior.Id,
                    "RECHAZADO",
                    "Entrega rechazada",
                    true)
            ],
            CancellationToken.None);

        var handler = new ActualizarReglasOperativasHandler(
            repositorio, motivos, unidad, new RelojFijo(Ahora));

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
        var motivosAnteriores = await motivos.ListarPorVersionAsync(
            operadorId,
            anterior.Id,
            CancellationToken.None);

        var motivosNuevos = await motivos.ListarPorVersionAsync(
            operadorId,
            nuevoId,
            CancellationToken.None);

        Assert.Equal(2, motivosAnteriores.Count);
        Assert.Equal(2, motivosNuevos.Count);

        Assert.Contains(motivosNuevos, m => m.Codigo == "AUSENTE");
        Assert.Contains(motivosNuevos, m => m.Codigo == "RECHAZADO");

        Assert.All(motivosNuevos, m =>
            Assert.Equal(nuevoId, m.VersionReglasOperativasId));
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

    private sealed class MotivosEnMemoria : IMotivoNoEntregaRepository
    {
        private readonly List<MotivoNoEntrega> _motivos = [];

        public Task<MotivoNoEntrega?> ObtenerPorCodigoAsync(
            Guid operadorId,
            Guid versionReglasOperativasId,
            string codigo,
            CancellationToken ct)
        {
            return Task.FromResult(_motivos.FirstOrDefault(m =>
                m.OperadorId == operadorId &&
                m.VersionReglasOperativasId == versionReglasOperativasId &&
                m.Codigo == codigo));
        }

        public Task<IReadOnlyList<MotivoNoEntrega>> ListarPorVersionAsync(
            Guid operadorId,
            Guid versionReglasOperativasId,
            CancellationToken ct)
        {
            IReadOnlyList<MotivoNoEntrega> resultado = _motivos
                .Where(m =>
                    m.OperadorId == operadorId &&
                    m.VersionReglasOperativasId == versionReglasOperativasId)
                .ToList();

            return Task.FromResult(resultado);
        }

        public Task AgregarRangoAsync(
            IEnumerable<MotivoNoEntrega> motivos,
            CancellationToken ct)
        {
            _motivos.AddRange(motivos);
            return Task.CompletedTask;
        }

        public Task<MotivoNoEntrega?> ObtenerPorDescripcionAsync(
            Guid operadorId,
            Guid versionReglasOperativasId,
            string descripcion,
            CancellationToken ct)
        {
            return Task.FromResult(_motivos.FirstOrDefault(m =>
                m.OperadorId == operadorId &&
                m.VersionReglasOperativasId == versionReglasOperativasId &&
                m.Descripcion == descripcion));
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
