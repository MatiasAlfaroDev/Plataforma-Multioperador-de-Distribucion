
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Common;
using UltimaMilla.Application.Envios.Commands.DeclararExtravio;
using UltimaMilla.Application.Envios.Commands.RegistrarReaparicion;
using UltimaMilla.Domain.Configuracion;
using UltimaMilla.Domain.Envios;
using UltimaMilla.Domain.Organizacion;
using UltimaMilla.Domain.Rutas;

namespace UltimaMilla.UnitTests;

public class ExtravioYReaparicionHandlerTests
{
    private static readonly DateTimeOffset Ahora =
        new(2026, 10, 30, 12, 0, 0, TimeSpan.Zero);

    private readonly Guid _operadorId = Guid.NewGuid();
    private readonly VersionReglasOperativas _reglas;
    private readonly List<Envio> _envios = [];
    private readonly List<VersionReglasOperativas> _versiones = [];
    private readonly HojasEnMemoria _hojas = new();
    private readonly UnidadEnMemoria _unidad = new();

    public ExtravioYReaparicionHandlerTests()
    {
        _reglas = VersionReglasOperativas.Crear(
            _operadorId, 3, 24, true, 7, Ahora.AddDays(-1));

        _versiones.Add(_reglas);
    }

    private Envio CrearEnvio(int cantidadBultos = 1)
    {
        var cuenta = CuentaComercial.Crear(
            Guid.NewGuid(), _operadorId, Guid.NewGuid());

        var bultos = Enumerable.Range(0, cantidadBultos)
            .Select(_ => new NuevoBulto(1m, 20, 20, 20))
            .ToList();

        var envio = Envio.Crear(
            cuenta,
            $"TEST-{Guid.NewGuid():N}",
            Modalidad.Estandar,
            new Destinatario("Destinatario", "099123456"),
            new Direccion("Calle", "123", "San José", "San José"),
            bultos,
            Ahora);

        envio.AsignarReglasOperativas(_reglas.Id);

        envio.Aplicar(
            DisparadorEnvio.RecibirEnDeposito,
            _reglas, Ahora, "Test", "Operario");

        _envios.Add(envio);
        return envio;
    }

    private HojaDeRuta CrearHoja(params Envio[] envios)
    {
        var repartidor = Repartidor.Crear(
            _operadorId, "Repartidor", "099123456");

        var vehiculo = Vehiculo.Crear(
            _operadorId, "TEST1234", 100m, 10m);

        var hoja = HojaDeRuta.Crear(
            _operadorId,
            repartidor,
            vehiculo,
            DateOnly.FromDateTime(Ahora.Date),
            5);

        foreach (var envio in envios)
        {
            hoja.AgregarEnvio(envio);

            envio.Aplicar(
                DisparadorEnvio.AsignarARuta,
                _reglas, Ahora, "Test", "Operario");
        }

        _hojas.Lista.Add(hoja);
        return hoja;
    }

    private DeclararExtravioHandler CrearHandlerExtravio() =>
        new(
            new EnviosEnMemoria(_envios),
            _hojas,
            new ReglasEnMemoria(_versiones),
            _unidad,
            new RelojFijo(Ahora.AddHours(1)));

    private RegistrarReaparicionHandler CrearHandlerReaparicion() =>
        new(
            new EnviosEnMemoria(_envios),
            new ReglasEnMemoria(_versiones),
            _unidad,
            new RelojFijo(Ahora.AddHours(2)));

    private async Task DeclararExtravio(Envio envio) =>
        await CrearHandlerExtravio().Handle(
            new DeclararExtravioCommand(
                _operadorId,
                envio.Id,
                "Bulto no localizado",
                "Administrador"),
            CancellationToken.None);

    [Fact]
    public async Task ExtravioEnDeposito_CambiaEstadoYRegistraMotivo()
    {
        var envio = CrearEnvio();

        await DeclararExtravio(envio);

        Assert.Equal(EstadoEnvio.Extraviado, envio.Estado);
        Assert.Equal(1, _unidad.Guardados);
        Assert.Equal(1, _unidad.Transacciones);

        var evento = envio.Eventos.Last();

        Assert.Equal(
            DisparadorEnvio.DeclararExtraviado,
            evento.Disparador);

        Assert.Equal("Bulto no localizado", evento.Justificacion);
        Assert.Equal("Administrador", evento.Responsable);
    }

    [Fact]
    public async Task ExtravioEnPreparacion_LiberaYRenumera()
    {
        var primero = CrearEnvio();
        var segundo = CrearEnvio();

        var hoja = CrearHoja(primero, segundo);
        var pesoInicial = hoja.PesoAsignadoKg;

        await DeclararExtravio(primero);

        Assert.Equal(EstadoEnvio.Extraviado, primero.Estado);
        Assert.Equal(EstadoEnvio.AsignadoARuta, segundo.Estado);

        Assert.False(hoja.Paradas.Single(
            p => p.EnvioId == primero.Id).Activa);

        var restante = hoja.Paradas.Single(
            p => p.EnvioId == segundo.Id);

        Assert.True(restante.Activa);
        Assert.Equal(1, restante.Orden);

        Assert.Equal(
            pesoInicial - primero.PesoTotalKg,
            hoja.PesoAsignadoKg);

        Assert.Equal(2, _unidad.Guardados);
        Assert.Equal(1, _unidad.Transacciones);
    }

    [Fact]
    public async Task ExtravioEnRutaDespachada_NoRenumera()
    {
        var primero = CrearEnvio();
        var segundo = CrearEnvio();

        var hoja = CrearHoja(primero, segundo);

        hoja.MarcarDespachada(Ahora);

        foreach (var envio in new[] { primero, segundo })
        {
            envio.Aplicar(
                DisparadorEnvio.Despachar,
                _reglas, Ahora, "Test", "Operario");
        }

        var ordenSegundo = hoja.Paradas.Single(
            p => p.EnvioId == segundo.Id).Orden;

        await DeclararExtravio(primero);

        Assert.Equal(EstadoEnvio.Extraviado, primero.Estado);
        Assert.Equal(EstadoEnvio.EnTransito, segundo.Estado);

        Assert.False(hoja.Paradas.Single(
            p => p.EnvioId == primero.Id).Activa);

        Assert.Equal(
            ordenSegundo,
            hoja.Paradas.Single(
                p => p.EnvioId == segundo.Id).Orden);

        Assert.Equal(EstadoHojaDeRuta.Despachada, hoja.Estado);
        Assert.Equal(1, _unidad.Guardados);
        Assert.Equal(1, _unidad.Transacciones);
    }

    [Fact]
    public async Task ExtravioSinJustificacion_NoModificaEnvio()
    {
        var envio = CrearEnvio();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            CrearHandlerExtravio().Handle(
                new DeclararExtravioCommand(
                    _operadorId, envio.Id, " ", "Administrador"),
                CancellationToken.None));

        Assert.Equal(EstadoEnvio.EnDeposito, envio.Estado);
        Assert.Equal(0, _unidad.Guardados);
    }

    [Fact]
    public async Task ExtravioDeOtroOperador_EsRechazado()
    {
        var envio = CrearEnvio();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CrearHandlerExtravio().Handle(
                new DeclararExtravioCommand(
                    Guid.NewGuid(),
                    envio.Id,
                    "Bulto no localizado",
                    "Administrador"),
                CancellationToken.None));

        Assert.Equal(EstadoEnvio.EnDeposito, envio.Estado);
        Assert.Equal(0, _unidad.Guardados);
    }

    [Fact]
    public async Task ReaparicionConTodosLosBultos_VuelveADeposito()
    {
        var envio = CrearEnvio(cantidadBultos: 2);

        await DeclararExtravio(envio);

        var codigos = envio.Bultos
            .Select(b => b.Identificacion)
            .ToArray();

        await CrearHandlerReaparicion().Handle(
            new RegistrarReaparicionCommand(
                _operadorId, envio.Id, codigos, "Deposito"),
            CancellationToken.None);

        Assert.Equal(EstadoEnvio.EnDeposito, envio.Estado);
        Assert.Equal(
            DisparadorEnvio.Reaparecer,
            envio.Eventos.Last().Disparador);

        Assert.Contains(envio.Eventos,
            e => e.Disparador == DisparadorEnvio.DeclararExtraviado &&
                 e.Justificacion == "Bulto no localizado");

        Assert.Equal(2, _unidad.Guardados);
    }

    [Fact]
    public async Task ReaparicionConBultoFaltante_EsRechazada()
    {
        var envio = CrearEnvio(cantidadBultos: 2);
        await DeclararExtravio(envio);

        var codigo = envio.Bultos.First().Identificacion;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CrearHandlerReaparicion().Handle(
                new RegistrarReaparicionCommand(
                    _operadorId, envio.Id, [codigo], "Deposito"),
                CancellationToken.None));

        Assert.Equal(EstadoEnvio.Extraviado, envio.Estado);
        Assert.Equal(1, _unidad.Guardados);
    }

    [Fact]
    public async Task ReaparicionConBultoDesconocido_EsRechazada()
    {
        var envio = CrearEnvio();
        await DeclararExtravio(envio);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CrearHandlerReaparicion().Handle(
                new RegistrarReaparicionCommand(
                    _operadorId,
                    envio.Id,
                    ["BULTO-INEXISTENTE"],
                    "Deposito"),
                CancellationToken.None));

        Assert.Equal(EstadoEnvio.Extraviado, envio.Estado);
        Assert.Equal(1, _unidad.Guardados);
    }

    [Fact]
    public async Task ReaparicionConBultosDuplicados_EsRechazada()
    {
        var envio = CrearEnvio();
        await DeclararExtravio(envio);

        var codigo = envio.Bultos.Single().Identificacion;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CrearHandlerReaparicion().Handle(
                new RegistrarReaparicionCommand(
                    _operadorId,
                    envio.Id,
                    [codigo, codigo],
                    "Deposito"),
                CancellationToken.None));

        Assert.Equal(EstadoEnvio.Extraviado, envio.Estado);
        Assert.Equal(1, _unidad.Guardados);
    }

    [Fact]
    public async Task ReaparicionDeEnvioNoExtraviado_EsRechazada()
    {
        var envio = CrearEnvio();
        var codigo = envio.Bultos.Single().Identificacion;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CrearHandlerReaparicion().Handle(
                new RegistrarReaparicionCommand(
                    _operadorId, envio.Id, [codigo], "Deposito"),
                CancellationToken.None));

        Assert.Equal(EstadoEnvio.EnDeposito, envio.Estado);
        Assert.Equal(0, _unidad.Guardados);
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
            Guid operadorId,
            Guid? cuentaComercialId,
            CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Envio>>(
                lista.Where(e =>
                    e.OperadorId == operadorId &&
                    (!cuentaComercialId.HasValue ||
                     e.CuentaComercialId == cuentaComercialId.Value))
                    .ToList());

        public Task<IReadOnlyList<Envio>> ListarParaPlanificacionAsync(
            Guid operadorId,
            CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Envio>>(
                lista.Where(e => e.OperadorId == operadorId).ToList());
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

        public Task<HojaDeRuta?> ObtenerPorEnvioActivoAsync(
            Guid operadorId, Guid envioId, CancellationToken ct) =>
            Task.FromResult(Lista.FirstOrDefault(h =>
                h.OperadorId == operadorId &&
                h.Paradas.Any(p =>
                    p.EnvioId == envioId && p.Activa)));

        public Task<IReadOnlyList<HojaDeRuta>> ListarDelRepartidorAsync(
            Guid operadorId,
            Guid repartidorId,
            DateOnly fecha,
            CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<HojaDeRuta>>(
                Lista.Where(h =>
                    h.OperadorId == operadorId &&
                    h.RepartidorId == repartidorId &&
                    h.Fecha == fecha)
                    .ToList());
    }

    private sealed class ReglasEnMemoria(
        List<VersionReglasOperativas> lista)
        : IVersionReglasOperativasRepository
    {
        public Task<VersionReglasOperativas?> ObtenerVigenteAsync(
            Guid operadorId,
            DateTimeOffset fecha,
            CancellationToken ct) =>
            Task.FromResult(lista.FirstOrDefault(r =>
                r.OperadorId == operadorId));

        public Task<VersionReglasOperativas?> ObtenerPorIdAsync(
            Guid operadorId,
            Guid versionId,
            CancellationToken ct) =>
            Task.FromResult(lista.FirstOrDefault(r =>
                r.OperadorId == operadorId && r.Id == versionId));

        public Task<VersionReglasOperativas?> ObtenerVigenteParaActualizarAsync(
            Guid operadorId,
            DateTimeOffset fecha,
            CancellationToken ct) =>
            Task.FromResult(lista
                .Where(r => r.OperadorId == operadorId && r.EstaVigente(fecha))
                .OrderByDescending(r => r.VigenciaDesde)
                .FirstOrDefault());

        public Task AgregarAsync(
            VersionReglasOperativas version,
            CancellationToken ct)
        {
            lista.Add(version);
            return Task.CompletedTask;
        }
    }

    private sealed class UnidadEnMemoria : IUnitOfWork
    {
        public int Guardados { get; private set; }
        public int Transacciones { get; private set; }

        public Task<int> GuardarCambiosAsync(CancellationToken ct)
        {
            Guardados++;
            return Task.FromResult(1);
        }

        public async Task EjecutarEnTransaccionAsync(
            Func<CancellationToken, Task> operacion,
            CancellationToken ct)
        {
            Transacciones++;
            await operacion(ct);
        }
    }

    private sealed class RelojFijo(DateTimeOffset ahora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => ahora;
    }
}
