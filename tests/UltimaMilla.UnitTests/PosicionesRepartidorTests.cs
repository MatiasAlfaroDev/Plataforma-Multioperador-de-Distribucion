using FluentValidation;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Common;
using UltimaMilla.Application.Rutas.Commands.RegistrarPosiciones;
using UltimaMilla.Application.Seguridad;
using UltimaMilla.Domain.Organizacion;
using UltimaMilla.Domain.Rutas;

namespace UltimaMilla.UnitTests;

/// <summary>Seguimiento de la posición del repartidor durante la jornada (RF 23).</summary>
public class PosicionesRepartidorTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 5, 15, 0, 0, TimeSpan.Zero);

    private readonly Guid _operadorId = Guid.NewGuid();
    private readonly Guid _usuarioId = Guid.NewGuid();
    private readonly Repartidor _repartidor;
    private readonly PosicionesEnMemoria _posiciones = new();
    private readonly UnidadDeTrabajoEnMemoria _unidad = new();
    private readonly SesionFalsa _sesion;
    private readonly RegistrarPosicionesHandler _handler;

    public PosicionesRepartidorTests()
    {
        _repartidor = Repartidor.Crear(_operadorId, "Ana Repartidora", "099111222");
        _repartidor.VincularUsuario(_usuarioId);
        _sesion = new SesionFalsa { UsuarioId = _usuarioId, OperadorId = _operadorId };

        _handler = new RegistrarPosicionesHandler(
            new RepartidoresEnMemoria([_repartidor]),
            _posiciones,
            _sesion,
            _unidad,
            new RegistrarPosicionesValidator(),
            new RelojFijo(Ahora));
    }

    private static PosicionCommand Posicion(Guid? id = null, int minutosAtras = 1) =>
        new(id ?? Guid.NewGuid(), -34.9011m, -56.1645m, Ahora.AddMinutes(-minutosAtras));

    [Fact]
    public async Task Guarda_el_lote_a_nombre_del_repartidor_logueado()
    {
        var guardadas = await _handler.Handle(
            new RegistrarPosicionesCommand([Posicion(), Posicion(minutosAtras: 2)]), CancellationToken.None);

        Assert.Equal(2, guardadas);
        Assert.All(_posiciones.Lista, p =>
        {
            Assert.Equal(_repartidor.Id, p.RepartidorId);
            Assert.Equal(_operadorId, p.OperadorId);
        });
        Assert.Equal(1, _unidad.Guardados);
    }

    [Fact]
    public async Task Reenviar_el_mismo_lote_no_duplica_posiciones()
    {
        var lote = new RegistrarPosicionesCommand([Posicion(), Posicion(minutosAtras: 2)]);

        await _handler.Handle(lote, CancellationToken.None);
        var segundaVez = await _handler.Handle(lote, CancellationToken.None);

        Assert.Equal(0, segundaVez);
        Assert.Equal(2, _posiciones.Lista.Count);
        Assert.Equal(1, _unidad.Guardados);
    }

    [Fact]
    public async Task Coordenadas_invalidas_se_rechazan()
    {
        var mala = new PosicionCommand(Guid.NewGuid(), 120m, -56m, Ahora);

        await Assert.ThrowsAsync<ValidationException>(() =>
            _handler.Handle(new RegistrarPosicionesCommand([mala]), CancellationToken.None));

        Assert.Empty(_posiciones.Lista);
    }

    [Fact]
    public async Task Usuario_sin_repartidor_vinculado_da_no_encontrado()
    {
        _sesion.UsuarioId = Guid.NewGuid();

        await Assert.ThrowsAsync<NoEncontradoException>(() =>
            _handler.Handle(new RegistrarPosicionesCommand([Posicion()]), CancellationToken.None));
    }

    [Fact]
    public void Una_posicion_del_futuro_se_rechaza()
    {
        Assert.Throws<ArgumentException>(() =>
            PosicionRepartidor.Registrar(Guid.NewGuid(), _operadorId, _repartidor.Id,
                -34.9m, -56.1m, Ahora.AddHours(1), Ahora));
    }

    // ----- Dobles de prueba -----

    private sealed class SesionFalsa : ITenantContext
    {
        public Guid? UsuarioId { get; set; }
        public Guid? OperadorId { get; set; }
        public Guid? ComercioId => null;
        public string? Perfil => Perfiles.Repartidor;
        public bool EstaAutenticado => UsuarioId is not null;
    }

    private sealed class RepartidoresEnMemoria(List<Repartidor> lista) : IRepartidorRepository
    {
        public Task<Repartidor?> ObtenerAsync(Guid operadorId, Guid repartidorId, CancellationToken ct) =>
            Task.FromResult(lista.FirstOrDefault(r => r.OperadorId == operadorId && r.Id == repartidorId));

        public Task<Repartidor?> ObtenerPorUsuarioAsync(Guid operadorId, Guid usuarioId, CancellationToken ct) =>
            Task.FromResult(lista.FirstOrDefault(r =>
                r.OperadorId == operadorId && r.UsuarioId == usuarioId && r.Activo));

        public Task<IReadOnlyList<Repartidor>> ListarActivosAsync(Guid operadorId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Repartidor>>(lista.Where(r => r.OperadorId == operadorId && r.Activo).ToList());
    }

    private sealed class PosicionesEnMemoria : IPosicionRepartidorRepository
    {
        public List<PosicionRepartidor> Lista { get; } = [];

        public Task<IReadOnlySet<Guid>> ExistentesAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
            Task.FromResult<IReadOnlySet<Guid>>(Lista.Select(p => p.Id).Where(ids.Contains).ToHashSet());

        public Task AgregarAsync(IEnumerable<PosicionRepartidor> posiciones, CancellationToken ct)
        {
            Lista.AddRange(posiciones);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<PosicionRepartidor>> ListarRecorridoAsync(
            Guid operadorId, Guid repartidorId, DateTimeOffset desde, DateTimeOffset hasta, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<PosicionRepartidor>>(Lista
                .Where(p => p.OperadorId == operadorId && p.RepartidorId == repartidorId &&
                            p.RegistradaEn >= desde && p.RegistradaEn < hasta)
                .OrderBy(p => p.RegistradaEn).ToList());
    }

    private sealed class UnidadDeTrabajoEnMemoria : IUnitOfWork
    {
        public int Guardados { get; private set; }

        public Task<int> GuardarCambiosAsync(CancellationToken ct)
        {
            Guardados++;
            return Task.FromResult(1);
        }

        public Task EjecutarEnTransaccionAsync(Func<CancellationToken, Task> operacion, CancellationToken ct) =>
            operacion(ct);
    }

    private sealed class RelojFijo(DateTimeOffset ahora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => ahora;
    }
}
