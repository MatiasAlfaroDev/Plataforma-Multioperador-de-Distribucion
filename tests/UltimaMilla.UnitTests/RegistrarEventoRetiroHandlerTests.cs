
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Avisos.Commands.RegistrarAvisosRetiro;
using UltimaMilla.Application.Common;
using UltimaMilla.Application.Retiros.Commands.RegistrarEventoRetiro;
using UltimaMilla.Application.Seguridad;
using UltimaMilla.Domain.Avisos;
using UltimaMilla.Domain.Envios;
using UltimaMilla.Domain.Organizacion;
using UltimaMilla.Domain.Retiros;
using UltimaMilla.Domain.Rutas;
using Xunit;

namespace UltimaMilla.UnitTests;

public sealed class RegistrarEventoRetiroHandlerTests
{
    private static readonly DateTimeOffset Ahora =
        new(2026, 11, 7, 16, 0, 0, TimeSpan.Zero);

    private readonly Guid _operadorId = Guid.NewGuid();
    private readonly Guid _usuarioId = Guid.NewGuid();

    private readonly Retiro _retiro;
    private readonly HojaDeRuta _hoja;
    private readonly Repartidor _repartidor;

    private readonly RetirosEnMemoria _retiros = new();
    private readonly HojasEnMemoria _hojas = new();
    private readonly RepartidoresEnMemoria _repartidores = new();
    private readonly EventosEnMemoria _eventos = new();
    private readonly UnidadDeTrabajoEnMemoria _unidad = new();

    private readonly RegistrarEventoRetiroHandler _handler;

    public RegistrarEventoRetiroHandlerTests()
    {
        _repartidor = Repartidor.Crear(
            _operadorId,
            "Repartidor de prueba",
            "099123456");

        var vehiculo = Vehiculo.Crear(
            _operadorId,
            "STP1234",
            100m,
            10m);

        _retiro = Retiro.Crear(
            _operadorId,
            Guid.NewGuid(),
            "Devolución de mercadería",
            new Direccion(
                "Artigas", "123", "San José de Mayo", "San José"),
            new Direccion(
                "Sarandí", "456", "San José de Mayo", "San José"),
            new DateOnly(2026, 11, 7),
            new TimeOnly(14, 0),
            new TimeOnly(18, 0),
            [
                BultoRetiro.Crear(
                    "BULTO-001", 2m, 20, 20, 20)
            ],
            Ahora.AddHours(-2));

        _hoja = HojaDeRuta.Crear(
            _operadorId,
            _repartidor,
            vehiculo,
            new DateOnly(2026, 11, 7),
            10);

        _hoja.ConfigurarHoraSalida(new TimeOnly(14, 0));
        _hoja.AgregarRetiro(_retiro);
        _retiro.AsignarARuta(_hoja.Id);
        _hoja.MarcarDespachada(Ahora.AddHours(-1));

        _retiros.Lista.Add(_retiro);
        _hojas.Lista.Add(_hoja);
        _repartidores.Lista.Add(_repartidor);

        var avisos = new RegistrarAvisosRetiroHandler(
            _retiros,
            new SuscripcionesEnMemoria(),
            new EntregasEnMemoria(),
            new PublicadorEnMemoria());

        _handler = new RegistrarEventoRetiroHandler(
            _retiros,
            _hojas,
            _repartidores,
            _eventos,
            _unidad,
            new SesionDePrueba(_usuarioId, _operadorId),
            new RelojFijo(Ahora),
            avisos);
    }

    private RegistrarEventoRetiroCommand ComandoExitoso() =>
        new(
            _retiro.Id,
            Guid.NewGuid(),
            "Retirado",
            Ahora,
            ["BULTO-001"],
            "foto.jpg",
            "firma.png",
            null);

    [Fact]
    public async Task Retiro_exitoso_finaliza_parada_y_guarda_evento()
    {
        var comando = ComandoExitoso();

        await _handler.Handle(comando, CancellationToken.None);

        Assert.Equal(EstadoRetiro.Retirado, _retiro.Estado);
        Assert.Equal(Ahora, _retiro.RetiradoEn);

        Assert.DoesNotContain(
            _hoja.Paradas,
            p => p.RetiroId == _retiro.Id && p.Activa);

        var evento = Assert.Single(_eventos.Lista);

        Assert.Equal(comando.IdEvento, evento.Id);
        Assert.Equal("Retirado", evento.Tipo);
        Assert.Equal("foto.jpg", evento.ReferenciaFotografia);
        Assert.Equal("firma.png", evento.ReferenciaFirma);
        Assert.Equal(1, _unidad.Guardados);
    }

    [Fact]
    public async Task Retiro_fallido_finaliza_parada()
    {
        var comando = new RegistrarEventoRetiroCommand(
            _retiro.Id,
            Guid.NewGuid(),
            "NoRetirado",
            Ahora,
            null,
            null,
            null,
            "No había nadie en el domicilio");

        await _handler.Handle(comando, CancellationToken.None);

        Assert.Equal(EstadoRetiro.NoRetirado, _retiro.Estado);

        Assert.DoesNotContain(
            _hoja.Paradas,
            p => p.RetiroId == _retiro.Id && p.Activa);

        Assert.Equal(
            "NoRetirado",
            Assert.Single(_eventos.Lista).Tipo);

        Assert.Equal(1, _unidad.Guardados);
    }

    [Fact]
    public async Task Evento_duplicado_no_modifica_el_retiro()
    {
        var comando = ComandoExitoso();

        await _handler.Handle(comando, CancellationToken.None);

        await Assert.ThrowsAsync<ConflictoException>(
            () => _handler.Handle(
                comando, CancellationToken.None));

        Assert.Single(_eventos.Lista);
        Assert.Equal(1, _unidad.Guardados);
    }

    [Fact]
    public async Task Repartidor_de_otra_hoja_es_rechazado()
    {
        // Simula que el usuario autenticado corresponde
        // a otro repartidor del mismo operador.
        _repartidores.Lista.Clear();

        _repartidores.Lista.Add(Repartidor.Crear(
            _operadorId,
            "Otro repartidor",
            "099999999"));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _handler.Handle(
                ComandoExitoso(),
                CancellationToken.None));

        Assert.Equal(
            EstadoRetiro.AsignadoARuta,
            _retiro.Estado);

        Assert.Empty(_eventos.Lista);
        Assert.Equal(0, _unidad.Guardados);
    }

    [Fact]
    public async Task Bultos_incorrectos_no_guardan_cambios()
    {
        var comando = ComandoExitoso() with
        {
            IdentificacionesBultos = ["BULTO-INEXISTENTE"]
        };

        await Assert.ThrowsAsync<ArgumentException>(
            () => _handler.Handle(
                comando, CancellationToken.None));

        Assert.Equal(
            EstadoRetiro.AsignadoARuta,
            _retiro.Estado);

        Assert.Contains(
            _hoja.Paradas,
            p => p.RetiroId == _retiro.Id && p.Activa);

        Assert.Empty(_eventos.Lista);
        Assert.Equal(0, _unidad.Guardados);
    }

    // Repositorios en memoria

    private sealed class RetirosEnMemoria : IRetiroRepository
    {
        public List<Retiro> Lista { get; } = [];

        public Task AgregarAsync(
            Retiro retiro, CancellationToken ct)
        {
            Lista.Add(retiro);
            return Task.CompletedTask;
        }

        public Task<Retiro?> ObtenerAsync(
            Guid id, CancellationToken ct) =>
            Task.FromResult(
                Lista.FirstOrDefault(r => r.Id == id));

        public Task<IReadOnlyList<Retiro>> ListarAsync(
            Guid operadorId,
            Guid? cuentaComercialId,
            CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Retiro>>(
                Lista.Where(r =>
                    r.OperadorId == operadorId &&
                    (!cuentaComercialId.HasValue ||
                     r.CuentaComercialId == cuentaComercialId.Value))
                .ToList());
        public Task<IReadOnlyList<Retiro>> ListarParaPlanificacionAsync(
            Guid operadorId,
            CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Retiro>>(
                Lista.Where(r =>
                    r.OperadorId == operadorId &&
                    r.Estado == EstadoRetiro.Solicitado &&
                    r.HojaDeRutaId == null)
                .OrderBy(r => r.FechaSolicitada)
                .ThenBy(r => r.HoraDesde)
                .ToList());

        public Task<IReadOnlyList<Retiro>> ListarParaEntregaComercioAsync(
            Guid operadorId,
            CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Retiro>>(
                Lista.Where(r =>
                    r.OperadorId == operadorId &&
                    r.Estado == EstadoRetiro.EnDeposito)
                .OrderBy(r => r.FechaSolicitada)
                .ThenBy(r => r.HoraDesde)
                .ToList());
    }

    private sealed class HojasEnMemoria : IHojaDeRutaRepository
    {
        public List<HojaDeRuta> Lista { get; } = [];

        public Task AgregarAsync(
            HojaDeRuta hoja, CancellationToken ct)
        {
            Lista.Add(hoja);
            return Task.CompletedTask;
        }

        public Task<HojaDeRuta?> ObtenerAsync(
            Guid operadorId,
            Guid hojaId,
            CancellationToken ct) =>
            Task.FromResult(Lista.FirstOrDefault(h =>
                h.Id == hojaId &&
                h.OperadorId == operadorId));

        public Task<IReadOnlyList<HojaDeRuta>>
            ListarDelRepartidorAsync(
                Guid operadorId,
                Guid repartidorId,
                DateOnly fecha,
                CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<HojaDeRuta>>(
                Lista.Where(h =>
                    h.OperadorId == operadorId &&
                    h.RepartidorId == repartidorId &&
                    h.Fecha == fecha).ToList());

        public Task<IReadOnlyList<HojaDeRuta>> ListarAsync(
            Guid operadorId,
            CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<HojaDeRuta>>(
                Lista.Where(h =>
                    h.OperadorId == operadorId).ToList());

        public Task<HojaDeRuta?> ObtenerPorEnvioActivoAsync(
            Guid operadorId,
            Guid envioId,
            CancellationToken ct) =>
            Task.FromResult(Lista.FirstOrDefault(h =>
                h.OperadorId == operadorId &&
                h.Paradas.Any(p =>
                    p.EnvioId == envioId && p.Activa)));

        public Task<HojaDeRuta?> ObtenerPorRetiroEnEntregaActivoAsync(
            Guid operadorId,
            Guid retiroId,
            CancellationToken ct)
        {
            return Task.FromResult<HojaDeRuta?>(null);
        }
    }

    private sealed class RepartidoresEnMemoria
        : IRepartidorRepository
    {
        public List<Repartidor> Lista { get; } = [];

        public Task<Repartidor?> ObtenerAsync(
            Guid operadorId,
            Guid repartidorId,
            CancellationToken ct) =>
            Task.FromResult(Lista.FirstOrDefault(r =>
                r.OperadorId == operadorId &&
                r.Id == repartidorId));

        public Task<Repartidor?> ObtenerPorUsuarioAsync(
            Guid operadorId,
            Guid usuarioId,
            CancellationToken ct) =>
            Task.FromResult(Lista.FirstOrDefault(r =>
                r.OperadorId == operadorId));

        public Task<IReadOnlyList<Repartidor>> ListarActivosAsync(
            Guid operadorId,
            CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Repartidor>>(
                Lista.Where(r =>
                    r.OperadorId == operadorId && r.Activo)
                .ToList());
    }

    private sealed class EventosEnMemoria
        : IEventoRetiroMovilRepository
    {
        public List<EventoRetiroMovil> Lista { get; } = [];

        public Task<bool> ExisteAsync(
            Guid idEvento,
            CancellationToken ct) =>
            Task.FromResult(Lista.Any(e =>
                e.Id == idEvento));

        public Task AgregarAsync(
            EventoRetiroMovil evento,
            CancellationToken ct)
        {
            Lista.Add(evento);
            return Task.CompletedTask;
        }
    }

    private sealed class UnidadDeTrabajoEnMemoria
        : IUnitOfWork
    {
        public int Guardados { get; private set; }

        public Task<int> GuardarCambiosAsync(
            CancellationToken ct)
        {
            Guardados++;
            return Task.FromResult(1);
        }

        public Task EjecutarEnTransaccionAsync(
            Func<CancellationToken, Task> operacion,
            CancellationToken ct) => operacion(ct);
    }

    private sealed class SesionDePrueba(
        Guid usuarioId,
        Guid operadorId) : ITenantContext
    {
        public Guid? UsuarioId => usuarioId;
        public Guid? OperadorId => operadorId;
        public Guid? ComercioId => null;
        public string? Perfil => Perfiles.Repartidor;
        public bool EstaAutenticado => true;
    }

    private sealed class RelojFijo(
        DateTimeOffset ahora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => ahora;
    }

    private sealed class SuscripcionesEnMemoria
        : ISuscripcionAvisoRepository
    {
        public Task<SuscripcionAviso?> ObtenerAsync(
            Guid id, CancellationToken ct) =>
            Task.FromResult<SuscripcionAviso?>(null);

        public Task<IReadOnlyList<SuscripcionAviso>> ListarAsync(
            IReadOnlyCollection<Guid>? cuentas,
            CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<SuscripcionAviso>>([]);

        public Task AgregarAsync(
            SuscripcionAviso suscripcion,
            CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class EntregasEnMemoria
        : IEntregaAvisoRepository
    {
        public Task<EntregaAviso?> ObtenerAsync(
            Guid id, CancellationToken ct) =>
            Task.FromResult<EntregaAviso?>(null);

        public Task<EntregaAviso?> ObtenerAsync(
            Guid id, IReadOnlyCollection<Guid>? cuentas, CancellationToken ct) =>
            Task.FromResult<EntregaAviso?>(null);

        public Task<bool> ExisteAsync(
            Guid suscripcionId,
            string claveEvento,
            CancellationToken ct) =>
            Task.FromResult(false);

        public Task<IReadOnlyList<EntregaAviso>> ListarAsync(
            IReadOnlyCollection<Guid>? cuentas,
            EstadoEntregaAviso? estado,
            CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<EntregaAviso>>([]);

        public Task<IReadOnlyList<EntregaAviso>> ListarFallidasAsync(
            CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<EntregaAviso>>([]);

        public Task<IReadOnlyList<Guid>> OperadoresConFallidasAsync(
            CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Guid>>([]);

        public Task AgregarAsync(
            EntregaAviso entrega,
            CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class PublicadorEnMemoria
        : IPublicadorDeEventos
    {
        public Task PublicarAsync<T>(
            T evento, CancellationToken ct)
            where T : class => Task.CompletedTask;
    }
}
