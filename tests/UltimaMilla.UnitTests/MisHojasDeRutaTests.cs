using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Common;
using UltimaMilla.Application.Rutas.Queries.ObtenerMisHojasDeRuta;
using UltimaMilla.Application.Seguridad;
using UltimaMilla.Domain.Configuracion;
using UltimaMilla.Domain.Envios;
using UltimaMilla.Domain.Organizacion;
using UltimaMilla.Domain.Rutas;

namespace UltimaMilla.UnitTests;

/// <summary>Repartidor vinculado a un usuario y descarga de su hoja de ruta desde la app.</summary>
public class MisHojasDeRutaTests
{
    private static readonly DateTimeOffset Alta = new(2026, 10, 28, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Hoy = DateOnly.FromDateTime(Alta.DateTime);

    private readonly Guid _operadorId = Guid.NewGuid();
    private readonly Guid _usuarioId = Guid.NewGuid();
    private readonly VersionReglasOperativas _reglas;
    private readonly Repartidor _repartidor;
    private readonly Vehiculo _vehiculo;

    private readonly List<Envio> _envios = [];
    private readonly List<HojaDeRuta> _hojas = [];
    private readonly List<Repartidor> _repartidores = [];
    private readonly SesionFalsa _sesion;
    private readonly ObtenerMisHojasDeRutaHandler _handler;

    public MisHojasDeRutaTests()
    {
        _reglas = VersionReglasOperativas.Crear(
            _operadorId,
            maximoIntentosEntrega: 3,
            horasMinimasEntreIntentos: 24,
            requierePruebaEntrega: true,
            diasMaximosParaDevolucion: 7,
            vigenciaDesde: Alta.AddDays(-1));

        _repartidor = Repartidor.Crear(_operadorId, "Ana Repartidora", "099111222");
        _repartidor.VincularUsuario(_usuarioId);
        _repartidores.Add(_repartidor);

        _vehiculo = Vehiculo.Crear(_operadorId, "STP1234", 100m, 10m);

        _sesion = new SesionFalsa { UsuarioId = _usuarioId, OperadorId = _operadorId };

        _handler = new ObtenerMisHojasDeRutaHandler(
            new RepartidoresEnMemoria(_repartidores),
            new HojasEnMemoria(_hojas),
            new EnviosEnMemoria(_envios),
            _sesion);
    }

    // ----- Dominio: vínculo repartidor ↔ usuario -----

    [Fact]
    public void Un_repartidor_se_vincula_a_un_usuario()
    {
        var repartidor = Repartidor.Crear(_operadorId, "Luis", "099");
        var usuario = Guid.NewGuid();

        repartidor.VincularUsuario(usuario);

        Assert.Equal(usuario, repartidor.UsuarioId);
    }

    [Fact]
    public void No_se_puede_pisar_el_vinculo_con_otro_usuario()
    {
        Assert.Throws<InvalidOperationException>(() => _repartidor.VincularUsuario(Guid.NewGuid()));
        Assert.Equal(_usuarioId, _repartidor.UsuarioId);
    }

    [Fact]
    public void Desvincular_deja_al_repartidor_sin_cuenta()
    {
        _repartidor.DesvincularUsuario();

        Assert.Null(_repartidor.UsuarioId);
    }

    // ----- Consulta: mi hoja de ruta -----

    [Fact]
    public async Task Devuelve_las_paradas_en_orden_con_sus_bultos()
    {
        var hoja = CrearHoja(Hoy);
        var primero = AgregarEnvio(hoja, "PED-001", bultos: 2);
        AgregarEnvio(hoja, "PED-002", bultos: 1);

        var resultado = await _handler.Handle(new ObtenerMisHojasDeRutaQuery(Hoy), CancellationToken.None);

        var mia = Assert.Single(resultado);
        Assert.Equal("Ana Repartidora", mia.Repartidor);
        Assert.Equal(new[] { "PED-001", "PED-002" }, mia.Paradas.Select(p => p.ReferenciaExterna));
        Assert.Equal(primero.Bultos.Select(b => b.Identificacion).OrderBy(i => i), mia.Paradas[0].Bultos);
        Assert.Equal(2, mia.Paradas[0].Bultos.Count);
    }

    [Fact]
    public async Task No_devuelve_las_hojas_de_otro_dia()
    {
        var ayer = CrearHoja(Hoy.AddDays(-1));
        AgregarEnvio(ayer, "PED-VIEJO", bultos: 1);

        var resultado = await _handler.Handle(new ObtenerMisHojasDeRutaQuery(Hoy), CancellationToken.None);

        Assert.Empty(resultado);
    }

    [Fact]
    public async Task No_devuelve_las_hojas_de_otro_repartidor()
    {
        var otro = Repartidor.Crear(_operadorId, "Otro", "098");
        _repartidores.Add(otro);
        var hojaAjena = HojaDeRuta.Crear(_operadorId, otro, _vehiculo, Hoy, 5);
        _hojas.Add(hojaAjena);
        AgregarEnvio(hojaAjena, "PED-AJENO", bultos: 1);

        var resultado = await _handler.Handle(new ObtenerMisHojasDeRutaQuery(Hoy), CancellationToken.None);

        Assert.Empty(resultado);
    }

    [Fact]
    public async Task Usuario_sin_repartidor_vinculado_da_no_encontrado()
    {
        _sesion.UsuarioId = Guid.NewGuid();

        await Assert.ThrowsAsync<NoEncontradoException>(() =>
            _handler.Handle(new ObtenerMisHojasDeRutaQuery(Hoy), CancellationToken.None));
    }

    // ----- Ayudas -----

    private HojaDeRuta CrearHoja(DateOnly fecha)
    {
        var hoja = HojaDeRuta.Crear(_operadorId, _repartidor, _vehiculo, fecha, 5);
        _hojas.Add(hoja);
        return hoja;
    }

    private Envio AgregarEnvio(HojaDeRuta hoja, string referencia, int bultos)
    {
        var cuenta = CuentaComercial.Crear(Guid.NewGuid(), _operadorId, Guid.NewGuid());

        var envio = Envio.Crear(
            cuenta,
            referencia,
            Modalidad.Estandar,
            new Destinatario("Destinatario Test", "099123456"),
            new Direccion("Calle Test", "123", "Montevideo", "Montevideo"),
            Enumerable.Range(0, bultos).Select(_ => new NuevoBulto(1m, 10, 10, 10)).ToList(),
            Alta);

        envio.AsignarReglasOperativas(_reglas.Id);
        envio.Aplicar(DisparadorEnvio.RecibirEnDeposito, _reglas, Alta, "Prueba", "OperarioTest");

        hoja.AgregarEnvio(envio);
        envio.Aplicar(DisparadorEnvio.AsignarARuta, _reglas, Alta, "Backoffice", "Despachador");

        _envios.Add(envio);
        return envio;
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

    private sealed class HojasEnMemoria(List<HojaDeRuta> lista) : IHojaDeRutaRepository
    {
        public Task AgregarAsync(HojaDeRuta hoja, CancellationToken ct)
        {
            lista.Add(hoja);
            return Task.CompletedTask;
        }

        public Task<HojaDeRuta?> ObtenerAsync(Guid operadorId, Guid hojaId, CancellationToken ct) =>
            Task.FromResult(lista.FirstOrDefault(h => h.Id == hojaId && h.OperadorId == operadorId));

        public Task<IReadOnlyList<HojaDeRuta>> ListarDelRepartidorAsync(
            Guid operadorId, Guid repartidorId, DateOnly fecha, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<HojaDeRuta>>(lista.Where(h =>
                h.OperadorId == operadorId && h.RepartidorId == repartidorId && h.Fecha == fecha).ToList());

        public Task<IReadOnlyList<HojaDeRuta>> ListarAsync(Guid operadorId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<HojaDeRuta>>(lista.Where(h => h.OperadorId == operadorId).ToList());
    }

    private sealed class EnviosEnMemoria(List<Envio> lista) : IEnvioRepository
    {
        public Task AgregarAsync(Envio envio, CancellationToken ct)
        {
            lista.Add(envio);
            return Task.CompletedTask;
        }

        public Task<Envio?> ObtenerAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(lista.FirstOrDefault(e => e.Id == id));

        public Task<bool> ExisteReferenciaAsync(Guid cuentaComercialId, string referenciaExterna, CancellationToken ct) =>
            Task.FromResult(lista.Any(e => e.CuentaComercialId == cuentaComercialId && e.ReferenciaExterna == referenciaExterna));

        public Task<IReadOnlyList<Envio>> ListarAsync(Guid operadorId, Guid? cuentaComercialId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Envio>>(lista.Where(e => e.OperadorId == operadorId).ToList());
    }
}
