using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Common;
using UltimaMilla.Application.Envios.Commands.RegistrarEventoMovil;
using UltimaMilla.Domain.Envios;
using UltimaMilla.Domain.Organizacion;

namespace UltimaMilla.UnitTests;

/// <summary>
/// Las reglas de la sincronización diferida, probadas con repositorios en memoria:
/// idempotencia (el mismo evento dos veces) y conflicto (dos entregas del mismo envío).
/// </summary>
public class RegistrarEventoMovilHandlerTests
{
    private static readonly DateTimeOffset Alta = new(2026, 10, 22, 12, 0, 0, TimeSpan.Zero);

    private readonly EnviosEnMemoria _envios = new();
    private readonly EventosEnMemoria _eventos = new();
    private readonly PublicadorEnMemoria _publicador = new();
    private readonly RegistrarEventoMovilHandler _handler;
    private readonly Envio _envio;

    public RegistrarEventoMovilHandlerTests()
    {
        _envio = Envio.Crear(
            CuentaComercial.Crear(Guid.NewGuid(), operadorId: Guid.NewGuid(), comercioId: Guid.NewGuid()),
            "PED-200", Modalidad.Estandar,
            new Destinatario("Ana Pérez", null),
            new Direccion("Av. Italia", "1234", "Montevideo", "Montevideo"),
            [new NuevoBulto(1m, 10, 10, 10)],
            Alta);
        _envios.Lista.Add(_envio);

        _handler = new RegistrarEventoMovilHandler(
            _envios, _eventos, new UnidadDeTrabajoEnMemoria(), _publicador,
            new RegistrarEventoMovilValidator(), new RelojFijo(Alta.AddHours(5)));
    }

    private RegistrarEventoMovilCommand Entrega(Guid idEvento, string receptor = "Ana") =>
        new(_envio.Id, idEvento, "Entregado", null, receptor, Alta.AddHours(2));

    [Fact]
    public async Task Entrega_valida_se_guarda_y_publica_el_evento()
    {
        await _handler.Handle(Entrega(Guid.NewGuid()), CancellationToken.None);

        Assert.Single(_eventos.Lista);
        Assert.Single(_publicador.Publicados);
    }

    [Fact]
    public async Task El_mismo_evento_dos_veces_da_conflicto_y_no_se_duplica()
    {
        var id = Guid.NewGuid();
        await _handler.Handle(Entrega(id), CancellationToken.None);

        await Assert.ThrowsAsync<ConflictoException>(() => _handler.Handle(Entrega(id), CancellationToken.None));
        Assert.Single(_eventos.Lista);
    }

    [Fact]
    public async Task Segunda_entrega_del_mismo_envio_desde_otro_evento_se_rechaza()
    {
        await _handler.Handle(Entrega(Guid.NewGuid(), "Ana"), CancellationToken.None);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.Handle(Entrega(Guid.NewGuid(), "Otra persona"), CancellationToken.None));
        Assert.Contains("ya figura como entregado", ex.Message);
        Assert.Single(_eventos.Lista);
    }

    [Fact]
    public async Task Envio_inexistente_da_no_encontrado()
    {
        var comando = Entrega(Guid.NewGuid()) with { EnvioId = Guid.NewGuid() };
        await Assert.ThrowsAsync<NoEncontradoException>(() => _handler.Handle(comando, CancellationToken.None));
    }

    // ----- Dobles de prueba -----

    private sealed class EnviosEnMemoria : IEnvioRepository
    {
        public List<Envio> Lista { get; } = [];
        public Task AgregarAsync(Envio envio, CancellationToken ct) { Lista.Add(envio); return Task.CompletedTask; }
        public Task<Envio?> ObtenerAsync(Guid id, CancellationToken ct) => Task.FromResult(Lista.FirstOrDefault(e => e.Id == id));
        public Task<bool> ExisteReferenciaAsync(Guid cuentaComercialId, string referenciaExterna, CancellationToken ct) =>
            Task.FromResult(Lista.Any(e => e.CuentaComercialId == cuentaComercialId && e.ReferenciaExterna == referenciaExterna));
        public Task<IReadOnlyList<Envio>> ListarAsync(
    Guid operadorId,
    Guid? cuentaComercialId,
    CancellationToken ct) =>
    Task.FromResult<IReadOnlyList<Envio>>(
        Lista.Where(e =>
            e.OperadorId == operadorId &&
            (!cuentaComercialId.HasValue ||
             e.CuentaComercialId == cuentaComercialId.Value))
        .ToList());
    }

    private sealed class EventosEnMemoria : IEventoMovilRepository
    {
        public List<EventoMovil> Lista { get; } = [];
        public Task<bool> ExisteAsync(Guid idEvento, CancellationToken ct) => Task.FromResult(Lista.Any(e => e.Id == idEvento));
        public Task<EventoMovil?> ObtenerEntregaAsync(Guid envioId, CancellationToken ct) =>
            Task.FromResult(Lista.FirstOrDefault(e => e.EnvioId == envioId && e.Tipo == TipoEventoMovil.Entregado));
        public Task AgregarAsync(EventoMovil evento, CancellationToken ct) { Lista.Add(evento); return Task.CompletedTask; }
    }

    private sealed class PublicadorEnMemoria : IPublicadorDeEventos
    {
        public List<object> Publicados { get; } = [];
        public Task PublicarAsync<T>(T evento, CancellationToken ct) where T : class { Publicados.Add(evento); return Task.CompletedTask; }
    }

    private sealed class UnidadDeTrabajoEnMemoria : IUnitOfWork
    {
        public Task<int> GuardarCambiosAsync(CancellationToken ct) => Task.FromResult(1);
    }

    private sealed class RelojFijo(DateTimeOffset ahora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => ahora;
    }
}
