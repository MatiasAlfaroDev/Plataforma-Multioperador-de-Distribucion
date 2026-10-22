
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Common;
using UltimaMilla.Application.Rutas.Commands.DespacharHojaDeRuta;
using UltimaMilla.Domain.Configuracion;
using UltimaMilla.Domain.Envios;
using UltimaMilla.Domain.Organizacion;
using UltimaMilla.Domain.Rutas;

namespace UltimaMilla.UnitTests;

public class DespacharHojaDeRutaHandlerTests
{
    private static readonly DateTimeOffset Alta =
        new(2026, 10, 22, 12, 0, 0, TimeSpan.Zero);

    private readonly Guid _operadorId = Guid.NewGuid();
    private readonly VersionReglasOperativas _reglas;
    private readonly List<Envio> _envios = [];
    private readonly List<VersionReglasOperativas> _versiones = [];
    private readonly HojasEnMemoria _hojas = new();
    private readonly UnidadDeTrabajoEnMemoria _unidad = new();

    private readonly HojaDeRuta _hoja;
    private readonly Envio _primero;
    private readonly Envio _segundo;
    private readonly DespacharHojaDeRutaHandler _handler;

    public DespacharHojaDeRutaHandlerTests()
    {
        _reglas = VersionReglasOperativas.Crear(
            _operadorId,
            maximoIntentosEntrega: 3,
            horasMinimasEntreIntentos: 24,
            requierePruebaEntrega: true,
            diasMaximosParaDevolucion: 7,
            vigenciaDesde: Alta.AddDays(-1));

        _versiones.Add(_reglas);

        _primero = CrearEnvio("TEST-001");
        _segundo = CrearEnvio("TEST-002");

        _envios.Add(_primero);
        _envios.Add(_segundo);

        _hoja = CrearHoja();
        AgregarAsignado(_hoja, _primero);
        AgregarAsignado(_hoja, _segundo);

        _hojas.Lista.Add(_hoja);

        _handler = new DespacharHojaDeRutaHandler(
            _hojas,
            new EnviosEnMemoria(_envios),
            new ReglasEnMemoria(_versiones),
            _unidad,
            new RelojFijo(Alta.AddHours(1)));
    }

    [Fact]
    public async Task DespacharHojaValida_CambiaTodosLosEnviosATransito()
    {
        var cantidad = await _handler.Handle(
            new DespacharHojaDeRutaCommand(
                _operadorId, _hoja.Id),
            CancellationToken.None);

        Assert.Equal(2, cantidad);
        Assert.Equal(EstadoEnvio.EnTransito, _primero.Estado);
        Assert.Equal(EstadoEnvio.EnTransito, _segundo.Estado);
        Assert.Equal(1, _unidad.Guardados);
    }

    [Fact]
    public async Task HojaSinParadasActivas_NoDespachaNiGuarda()
    {
        var hojaVacia = CrearHoja();
        _hojas.Lista.Add(hojaVacia);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Handle(
                new DespacharHojaDeRutaCommand(
                    _operadorId, hojaVacia.Id),
                CancellationToken.None));

        Assert.Equal(EstadoEnvio.AsignadoARuta, _primero.Estado);
        Assert.Equal(0, _unidad.Guardados);
    }

    [Fact]
    public async Task EnvioInvalido_NoDespachaParcialmenteLaHoja()
    {
        // Simula una inconsistencia en los datos persistidos:
        // la parada sigue activa, pero el envío ya no está asignado.
        _segundo.Aplicar(
            DisparadorEnvio.QuitarDeRuta,
            _reglas,
            Alta.AddMinutes(1),
            "Prueba",
            "OperarioTest");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Handle(
                new DespacharHojaDeRutaCommand(
                    _operadorId, _hoja.Id),
                CancellationToken.None));

        Assert.Equal(EstadoEnvio.AsignadoARuta, _primero.Estado);
        Assert.Equal(EstadoEnvio.EnDeposito, _segundo.Estado);
        Assert.Equal(0, _unidad.Guardados);
    }

    [Fact]
    public async Task HojaDeOtroOperador_NoSeEncuentra()
    {
        await Assert.ThrowsAsync<NoEncontradoException>(
            () => _handler.Handle(
                new DespacharHojaDeRutaCommand(
                    Guid.NewGuid(), _hoja.Id),
                CancellationToken.None));

        Assert.Equal(EstadoEnvio.AsignadoARuta, _primero.Estado);
        Assert.Equal(0, _unidad.Guardados);
    }

    private Envio CrearEnvio(string referencia)
    {
        var cuenta = CuentaComercial.Crear(
            Guid.NewGuid(),
            _operadorId,
            Guid.NewGuid());

        var envio = Envio.Crear(
            cuenta,
            referencia,
            Modalidad.Estandar,
            new Destinatario("Destinatario Test", "099123456"),
            new Direccion(
                "Calle Test", "123", "Montevideo", "Montevideo"),
            [new NuevoBulto(1m, 10, 10, 10)],
            Alta);

        envio.AsignarReglasOperativas(_reglas.Id);

        envio.Aplicar(
            DisparadorEnvio.RecibirEnDeposito,
            _reglas,
            Alta,
            "Prueba",
            "OperarioTest");

        return envio;
    }

    private HojaDeRuta CrearHoja()
    {
        var repartidor = Repartidor.Crear(
            _operadorId, "Repartidor Test", "099123456");

        var vehiculo = Vehiculo.Crear(
            _operadorId, "STP1234", 100m, 10m);

        return HojaDeRuta.Crear(
            _operadorId,
            repartidor,
            vehiculo,
            DateOnly.FromDateTime(Alta.DateTime),
            5);
    }

    private void AgregarAsignado(HojaDeRuta hoja, Envio envio)
    {
        hoja.AgregarEnvio(envio);

        envio.Aplicar(
            DisparadorEnvio.AsignarARuta,
            _reglas,
            Alta,
            "Backoffice",
            "Despachador");
    }

    private sealed class EnviosEnMemoria(List<Envio> lista)
        : IEnvioRepository
    {
        public Task AgregarAsync(Envio envio, CancellationToken ct)
        {
            lista.Add(envio);
            return Task.CompletedTask;
        }

        public Task<Envio?> ObtenerAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(lista.FirstOrDefault(e => e.Id == id));

        public Task<bool> ExisteReferenciaAsync(
            Guid cuentaComercialId,
            string referenciaExterna,
            CancellationToken ct) =>
            Task.FromResult(lista.Any(e =>
                e.CuentaComercialId == cuentaComercialId &&
                e.ReferenciaExterna == referenciaExterna));

        public Task<IReadOnlyList<Envio>> ListarAsync(
            Guid? operadorId,
            CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Envio>>(
                lista.Where(e =>
                    operadorId is null || e.OperadorId == operadorId)
                .ToList());
    }

    private sealed class HojasEnMemoria : IHojaDeRutaRepository
    {
        public List<HojaDeRuta> Lista { get; } = [];

        public Task AgregarAsync(HojaDeRuta hoja, CancellationToken ct)
        {
            Lista.Add(hoja);
            return Task.CompletedTask;
        }

        public Task<HojaDeRuta?> ObtenerAsync(
            Guid operadorId, Guid hojaId, CancellationToken ct) =>
            Task.FromResult(Lista.FirstOrDefault(h =>
                h.Id == hojaId && h.OperadorId == operadorId));

        public Task<IReadOnlyList<HojaDeRuta>> ListarAsync(
            Guid operadorId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<HojaDeRuta>>(
                Lista.Where(h => h.OperadorId == operadorId).ToList());
    }

    private sealed class ReglasEnMemoria(
        List<VersionReglasOperativas> lista)
        : IVersionReglasOperativasRepository
    {
        public Task<VersionReglasOperativas?> ObtenerVigenteAsync(
            Guid operadorId, DateTimeOffset fecha, CancellationToken ct) =>
            Task.FromResult<VersionReglasOperativas?>(lista
                .Where(r => r.OperadorId == operadorId)
                .FirstOrDefault());

        public Task<VersionReglasOperativas?> ObtenerPorIdAsync(
            Guid operadorId, Guid versionId, CancellationToken ct) =>
            Task.FromResult(lista.FirstOrDefault(r =>
                r.OperadorId == operadorId && r.Id == versionId));
    }

    private sealed class UnidadDeTrabajoEnMemoria : IUnitOfWork
    {
        public int Guardados { get; private set; }

        public Task<int> GuardarCambiosAsync(CancellationToken ct)
        {
            Guardados++;
            return Task.FromResult(1);
        }
    }

    private sealed class RelojFijo(DateTimeOffset ahora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => ahora;
    }
}
