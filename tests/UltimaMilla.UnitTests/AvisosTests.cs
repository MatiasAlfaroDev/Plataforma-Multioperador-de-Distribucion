using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentValidation;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Avisos;
using UltimaMilla.Application.Avisos.Commands.EntregarAviso;
using UltimaMilla.Application.Avisos.Commands.RegistrarAvisos;
using UltimaMilla.Application.Avisos.Commands.ReintentarAvisosFallidos;
using UltimaMilla.Application.Avisos.Eventos;
using UltimaMilla.Domain.Avisos;
using UltimaMilla.Domain.Envios;
using UltimaMilla.Domain.Organizacion;

namespace UltimaMilla.UnitTests;

/// <summary>
/// Avisos a comercios (RF 28, RNF 6.8 y 6.9): suscripción, estados de la entrega, idempotencia y firma.
/// </summary>
public class AvisosTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);
    private static readonly Destinatario Ana = new("Ana Pérez", "099123456");
    private static readonly Direccion Casa = new("Av. Italia", "1234", "Montevideo", "Montevideo");
    private static readonly NuevoBulto Caja = new(2.5m, 20, 30, 40);

    private readonly Envio _envio = Envio.Crear(
        CuentaComercial.Crear(Guid.NewGuid(), operadorId: Guid.NewGuid(), comercioId: Guid.NewGuid()),
        "PED-100",
        Modalidad.Estandar,
        Ana,
        Casa,
        [Caja],
        Ahora);

    private readonly SuscripcionesEnMemoria _suscripciones = new();
    private readonly EntregasEnMemoria _entregas = new();
    private readonly PublicadorEnMemoria _publicador = new();
    private readonly UnidadDeTrabajoEnMemoria _unidad = new();
    private readonly ClienteFalso _cliente = new();

    [Fact]
    public void Cada_estado_salvo_Admitido_tiene_un_tipo_de_aviso_suscribible()
    {
        foreach (var estado in Enum.GetValues<EstadoEnvio>().Where(e => e != EstadoEnvio.Admitido))
            Assert.Contains(TiposAviso.DeEstado(estado), TiposAviso.Todos);

        // El alta se avisa por EnvioCreado; sin esto llegarían dos avisos por cada envío nuevo.
        Assert.Null(TiposAviso.DeEstado(EstadoEnvio.Admitido));
    }

    private RegistrarAvisosHandler Registrar() =>
        new(new EnviosEnMemoria(_envio), _suscripciones, _entregas, _publicador, _unidad);

    private EntregarAvisoHandler Entregar() =>
        new(_entregas, _suscripciones, _cliente, _unidad, new RelojFijo(Ahora));

    private SuscripcionAviso Suscripcion(params string[] tipos) => SuscripcionAviso.Crear(
        _envio.OperadorId,
        _envio.CuentaComercialId,
        "https://comercio.test/avisos",
        "secreto",
        tipos.Length == 0 ? TiposAviso.Todos : tipos);

    // ----- Suscripción -----

    [Fact]
    public void Suscripcion_con_url_que_no_es_http_se_rechaza()
    {
        Assert.Throws<ArgumentException>(() =>
            SuscripcionAviso.Crear(Guid.NewGuid(), Guid.NewGuid(), "ftp://x", "secreto", TiposAviso.Todos));
    }

    [Fact]
    public void Suscripcion_con_tipo_desconocido_se_rechaza()
    {
        Assert.Throws<ArgumentException>(() =>
            SuscripcionAviso.Crear(Guid.NewGuid(), Guid.NewGuid(), "https://x", "secreto", ["envio.inventado"]));
    }

    [Fact]
    public void Suscripcion_sin_tipos_se_rechaza()
    {
        Assert.Throws<ArgumentException>(() =>
            SuscripcionAviso.Crear(Guid.NewGuid(), Guid.NewGuid(), "https://x", "secreto", []));
    }

    // ----- Estados de la entrega -----

    [Fact]
    public void Un_fallo_suma_el_intento_y_deja_la_entrega_fallida()
    {
        var entrega = EntregaAviso.Crear(Suscripcion(TiposAviso.EnvioCreado), TiposAviso.EnvioCreado, "clave", "{}");

        entrega.RegistrarFallo("Error 1", Ahora);
        entrega.RegistrarFallo("Error 2", Ahora);

        Assert.Equal(2, entrega.Intentos);
        Assert.Equal(EstadoEntregaAviso.Fallida, entrega.Estado);
        Assert.Equal("Error 2", entrega.UltimoError);
    }

    [Fact]
    public void El_exito_despues_de_un_fallo_limpia_el_error()
    {
        var entrega = EntregaAviso.Crear(Suscripcion(TiposAviso.EnvioCreado), TiposAviso.EnvioCreado, "clave", "{}");
        entrega.RegistrarFallo("Error", Ahora);
        entrega.RegistrarExito(Ahora);

        Assert.Equal(EstadoEntregaAviso.Entregada, entrega.Estado);
        Assert.Equal(Ahora, entrega.EntregadaEn);
        Assert.Null(entrega.UltimoError);
    }

    [Fact]
    public void Solo_se_reencola_una_entrega_fallida()
    {
        var entrega = EntregaAviso.Crear(Suscripcion(TiposAviso.EnvioCreado), TiposAviso.EnvioCreado, "clave", "{}");
        Assert.Throws<InvalidOperationException>(() => entrega.Reencolar());

        entrega.RegistrarFallo("Error", Ahora);
        entrega.Reencolar();

        Assert.Equal(EstadoEntregaAviso.Pendiente, entrega.Estado);
    }

    [Fact]
    public void Una_suscripcion_no_genera_avisos_de_tipos_que_no_escucha()
    {
        var suscripcion = Suscripcion(TiposAviso.EnvioEntregado);
        Assert.Throws<InvalidOperationException>(() =>
            EntregaAviso.Crear(suscripcion, TiposAviso.EnvioCreado, "clave", "{}"));
    }

    // ----- RegistrarAvisos -----

    [Fact]
    public async Task Registrar_crea_un_aviso_por_suscripcion_que_escucha_el_tipo()
    {
        _suscripciones.Lista.Add(Suscripcion());
        _suscripciones.Lista.Add(Suscripcion(TiposAviso.EnvioEntregado));

        var resultado = await Registrar().Handle(
            new RegistrarAvisosCommand(_envio.Id, TiposAviso.EnvioCreado, "envio.creado:1", Ahora),
            CancellationToken.None);

        Assert.Equal(1, resultado);
        Assert.Single(_entregas.Lista);
        var entrega = Assert.Single(_entregas.Lista);
        Assert.Equal(TiposAviso.EnvioCreado, entrega.TipoEvento);
        Assert.Equal("envio.creado:1", entrega.ClaveEvento);
        Assert.Single(_publicador.Publicados);
        Assert.IsType<AvisoPendiente>(_publicador.Publicados[0]);
        Assert.Equal(1, _unidad.Guardados);

        var json = JsonDocument.Parse(entrega.Cuerpo);
        Assert.Equal("envio.creado", json.RootElement.GetProperty("tipo").GetString());
        Assert.Equal(_envio.ReferenciaExterna, json.RootElement.GetProperty("referenciaExterna").GetString());
    }

    [Fact]
    public async Task Procesar_dos_veces_el_mismo_evento_no_duplica_el_aviso()
    {
        _suscripciones.Lista.Add(Suscripcion());

        await Registrar().Handle(
            new RegistrarAvisosCommand(_envio.Id, TiposAviso.EnvioCreado, "clave", Ahora),
            CancellationToken.None);

        var resultado = await Registrar().Handle(
            new RegistrarAvisosCommand(_envio.Id, TiposAviso.EnvioCreado, "clave", Ahora),
            CancellationToken.None);

        Assert.Equal(0, resultado);
        Assert.Single(_entregas.Lista);
        Assert.Single(_publicador.Publicados);
    }

    // ----- EntregarAviso -----

    [Fact]
    public async Task Entregar_con_exito_marca_la_entrega_como_entregada()
    {
        var entrega = AgregarEntregaPendiente();

        await Entregar().Handle(new EntregarAvisoCommand(entrega.Id), CancellationToken.None);

        Assert.Equal(EstadoEntregaAviso.Entregada, entrega.Estado);
        Assert.Equal(1, entrega.Intentos);
        Assert.Equal(entrega.Cuerpo, _cliente.UltimoCuerpo);
    }

    [Fact]
    public async Task Si_el_receptor_falla_se_guarda_el_fallo_y_se_relanza_para_que_reintente_el_worker()
    {
        _cliente.Falla = true;

        var entrega = AgregarEntregaPendiente();

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() =>
            Entregar().Handle(new EntregarAvisoCommand(entrega.Id), CancellationToken.None));

        Assert.Contains("500", ex.Message);
        Assert.Equal(EstadoEntregaAviso.Fallida, entrega.Estado);
        Assert.Equal(1, entrega.Intentos);
        Assert.Equal(1, _unidad.Guardados);
    }

    [Fact]
    public async Task Una_entrega_ya_entregada_no_se_vuelve_a_enviar()
    {
        var entrega = AgregarEntregaPendiente();
        await Entregar().Handle(new EntregarAvisoCommand(entrega.Id), CancellationToken.None);

        // Redelivery del mismo AvisoPendiente: no llama de nuevo al receptor.
        await Entregar().Handle(new EntregarAvisoCommand(entrega.Id), CancellationToken.None);

        Assert.Equal(1, _cliente.Llamadas);
    }

    // ----- Firma -----

    [Fact]
    public void La_firma_es_el_hmac_sha256_del_cuerpo_con_el_secreto()
    {
        var cuerpo = "hola";
        var secreto = "clave";

        var firma = FirmaAviso.Calcular("clave", cuerpo);

        var esperado = Convert.ToHexString(
            HMACSHA256.HashData(Encoding.UTF8.GetBytes(secreto), Encoding.UTF8.GetBytes(cuerpo)))
            .ToLowerInvariant();

        Assert.Equal("sha256=" + esperado, firma);
        Assert.NotEqual("sha256=" + esperado, FirmaAviso.Calcular("otraclave", cuerpo));
    }

    private EntregaAviso AgregarEntregaPendiente()
    {
        var suscripcion = Suscripcion(TiposAviso.EnvioCreado);
        _suscripciones.Lista.Add(suscripcion);

        var entrega = EntregaAviso.Crear(suscripcion, TiposAviso.EnvioCreado, "clave", "{}");
        _entregas.Lista.Add(entrega);
        return entrega;
    }

    // ----- Reintento programado -----

    private static EntregaAviso Fallida(EntregaAviso entrega, int intentos, DateTimeOffset ultimo)
    {
        for (var i = 0; i < intentos; i++)
            entrega.RegistrarFallo("HTTP 500", ultimo);
        return entrega;
    }

    [Fact]
    public void Fallida_se_reintenta_recien_cumplida_la_espera_de_su_ronda()
    {
        var entrega = Fallida(AgregarEntregaPendiente(), EntregaAviso.IntentosPorRonda, Ahora);

        // Primera ronda: 2 minutos, más que la ventana de los reintentos en memoria.
        Assert.False(entrega.PuedeReintentar(Ahora.AddMinutes(1)));
        Assert.True(entrega.PuedeReintentar(Ahora.AddMinutes(2)));
    }

    [Fact]
    public void La_espera_crece_con_las_rondas()
    {
        var entrega = Fallida(AgregarEntregaPendiente(), EntregaAviso.IntentosPorRonda * 3, Ahora);

        Assert.False(entrega.PuedeReintentar(Ahora.AddMinutes(14)));
        Assert.True(entrega.PuedeReintentar(Ahora.AddMinutes(15)));
    }

    [Fact]
    public void Agotada_no_se_reintenta_sola()
    {
        var entrega = Fallida(AgregarEntregaPendiente(), EntregaAviso.MaximoIntentos, Ahora);

        Assert.True(entrega.Agotada);
        Assert.False(entrega.PuedeReintentar(Ahora.AddDays(1)));
    }

    [Fact]
    public async Task Reintento_programado_reencola_y_publica_solo_las_vencidas()
    {
        var vencida = Fallida(AgregarEntregaPendiente(), EntregaAviso.IntentosPorRonda, Ahora.AddMinutes(-10));
        var reciente = Fallida(AgregarEntregaPendiente(), EntregaAviso.IntentosPorRonda, Ahora);

        var n = await new ReintentarAvisosFallidosHandler(_entregas, _publicador, _unidad, new RelojFijo(Ahora))
            .Handle(new ReintentarAvisosFallidosCommand(), CancellationToken.None);

        Assert.Equal(1, n);
        Assert.Equal(EstadoEntregaAviso.Pendiente, vencida.Estado);
        Assert.Equal(EstadoEntregaAviso.Fallida, reciente.Estado);
        Assert.Equal(vencida.Id, Assert.IsType<AvisoPendiente>(Assert.Single(_publicador.Publicados)).EntregaAvisoId);
        Assert.Equal(1, _unidad.Guardados);
    }

    // ----- Dobles de prueba -----

    private sealed class EnviosEnMemoria(Envio envio) : IEnvioRepository
    {
        public Task<Envio?> ObtenerAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(id == envio.Id ? envio : null);

        public Task<bool> ExisteReferenciaAsync(Guid cuentaComercialId, string referenciaExterna, CancellationToken ct) =>
            Task.FromResult(false);

        public Task<IReadOnlyList<Envio>> ListarAsync(Guid operadorId, Guid? cuentaComercialId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Envio>>([envio]);

        public Task<IReadOnlyList<Envio>> ListarParaPlanificacionAsync(Guid operadorId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Envio>>([envio]);

        public Task AgregarAsync(Envio envio, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class SuscripcionesEnMemoria : ISuscripcionAvisoRepository
    {
        public List<SuscripcionAviso> Lista { get; } = [];

        public Task<SuscripcionAviso?> ObtenerAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(Lista.FirstOrDefault(s => s.Id == id));

        public Task<IReadOnlyList<SuscripcionAviso>> ListarAsync(Guid? cuentaComercialId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<SuscripcionAviso>>(
                Lista.Where(s => cuentaComercialId is null || s.CuentaComercialId == cuentaComercialId).ToList());

        public Task AgregarAsync(SuscripcionAviso suscripcion, CancellationToken ct)
        {
            Lista.Add(suscripcion);
            return Task.CompletedTask;
        }
    }

    private sealed class EntregasEnMemoria : IEntregaAvisoRepository
    {
        public List<EntregaAviso> Lista { get; } = [];

        public Task<EntregaAviso?> ObtenerAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(Lista.FirstOrDefault(e => e.Id == id));

        public Task<bool> ExisteAsync(Guid suscripcionId, string claveEvento, CancellationToken ct) =>
            Task.FromResult(Lista.Any(e => e.SuscripcionId == suscripcionId && e.ClaveEvento == claveEvento));

        public Task<IReadOnlyList<EntregaAviso>> ListarAsync(Guid? cuentaComercialId, EstadoEntregaAviso? estado, CancellationToken ct)
        {
            var query = Lista.AsQueryable();
            if (cuentaComercialId is not null)
                query = query.Where(e => e.CuentaComercialId == cuentaComercialId);
            if (estado is not null)
                query = query.Where(e => e.Estado == estado);

            return Task.FromResult<IReadOnlyList<EntregaAviso>>(query.ToList());
        }

        public Task<IReadOnlyList<EntregaAviso>> ListarFallidasAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<EntregaAviso>>(
                Lista.Where(e => e.Estado == EstadoEntregaAviso.Fallida && !e.Agotada).ToList());

        public Task<IReadOnlyList<Guid>> OperadoresConFallidasAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Guid>>(
                Lista.Where(e => e.Estado == EstadoEntregaAviso.Fallida && !e.Agotada).Select(e => e.OperadorId).Distinct().ToList());

        public Task AgregarAsync(EntregaAviso entrega, CancellationToken ct)
        {
            Lista.Add(entrega);
            return Task.CompletedTask;
        }
    }

    private sealed class PublicadorEnMemoria : IPublicadorDeEventos
    {
        public List<object> Publicados { get; } = [];

        public Task PublicarAsync<T>(T evento, CancellationToken ct) where T : class
        {
            Publicados.Add(evento);
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

        public Task EjecutarEnTransaccionAsync(Func<CancellationToken, Task> operacion, CancellationToken ct) =>
            operacion(ct);
    }

    private sealed class RelojFijo(DateTimeOffset ahora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => ahora;
    }

    private sealed class ClienteFalso : IClienteAvisos
    {
        public bool Falla { get; set; }
        public int Llamadas { get; private set; }
        public string? UltimoCuerpo { get; private set; }

        public Task EnviarAsync(string url, string secreto, Guid entregaId, string tipoEvento, string cuerpo, CancellationToken ct)
        {
            if (Falla)
                throw new HttpRequestException("El receptor respondió HTTP 500.");

            Llamadas++;
            UltimoCuerpo = cuerpo;
            return Task.CompletedTask;
        }
    }
}
