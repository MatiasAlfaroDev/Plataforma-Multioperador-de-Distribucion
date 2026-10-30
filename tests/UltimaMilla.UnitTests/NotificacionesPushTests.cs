using FluentValidation;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Common;
using UltimaMilla.Application.Notificaciones.Commands.EnviarNotificacionDePrueba;
using UltimaMilla.Application.Notificaciones.Commands.RegistrarDispositivo;
using UltimaMilla.Application.Seguridad;
using UltimaMilla.Domain.Notificaciones;

namespace UltimaMilla.UnitTests;

/// <summary>Registro de teléfonos y envío de avisos push (ciclo de vida del token).</summary>
public class NotificacionesPushTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 10, 28, 12, 0, 0, TimeSpan.Zero);

    private readonly SesionFalsa _sesion = new();
    private readonly DispositivosEnMemoria _dispositivos = new();
    private readonly NotificadorFalso _notificador = new();
    private readonly UnidadDeTrabajoEnMemoria _unidad = new();

    private RegistrarDispositivoHandler Registrar() =>
        new(_dispositivos, _sesion, _unidad, new RegistrarDispositivoValidator(), new RelojFijo(Ahora));

    private EnviarNotificacionDePruebaHandler Enviar() =>
        new(_dispositivos, _notificador, _sesion, _unidad);

    // ----- Registro -----

    [Fact]
    public async Task Registrar_un_telefono_nuevo_lo_guarda_con_el_usuario_y_operador_de_la_sesion()
    {
        await Registrar().Handle(new RegistrarDispositivoCommand("token-1", "Android"), CancellationToken.None);

        var guardado = Assert.Single(_dispositivos.Lista);
        Assert.Equal(_sesion.UsuarioId, guardado.UsuarioId);
        Assert.Equal(_sesion.OperadorId, guardado.OperadorId);
        Assert.Equal("token-1", guardado.Token);
        Assert.Equal(1, _unidad.Guardados);
    }

    [Fact]
    public async Task Registrar_el_mismo_token_otra_vez_lo_renueva_sin_duplicarlo()
    {
        await Registrar().Handle(new RegistrarDispositivoCommand("token-1", "Android"), CancellationToken.None);

        // En el mismo teléfono inicia sesión otro repartidor.
        var otroUsuario = Guid.NewGuid();
        _sesion.UsuarioId = otroUsuario;
        await Registrar().Handle(new RegistrarDispositivoCommand("token-1", "Android"), CancellationToken.None);

        var guardado = Assert.Single(_dispositivos.Lista);
        Assert.Equal(otroUsuario, guardado.UsuarioId);
    }

    [Fact]
    public async Task Registrar_sin_token_se_rechaza()
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            Registrar().Handle(new RegistrarDispositivoCommand("", "Android"), CancellationToken.None));

        Assert.Empty(_dispositivos.Lista);
        Assert.Equal(0, _unidad.Guardados);
    }

    [Fact]
    public async Task Registrar_sin_operador_elegido_se_rechaza()
    {
        _sesion.OperadorId = null;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Registrar().Handle(new RegistrarDispositivoCommand("token-1", "Android"), CancellationToken.None));

        Assert.Empty(_dispositivos.Lista);
    }

    // ----- Envío -----

    [Fact]
    public async Task Aviso_de_prueba_llega_a_todos_los_telefonos_del_usuario()
    {
        AgregarTelefono("token-1");
        AgregarTelefono("token-2");

        var resultado = await Enviar().Handle(
            new EnviarNotificacionDePruebaCommand("Reasignación urgente", "PED-300"), CancellationToken.None);

        Assert.Equal(2, resultado.Enviadas);
        Assert.Equal(new[] { "token-1", "token-2" }, _notificador.Enviados.Select(e => e.Token));
        Assert.All(_notificador.Enviados, e => Assert.Equal("Reasignación urgente", e.Notificacion.Titulo));
    }

    [Fact]
    public async Task Token_que_Firebase_ya_no_reconoce_se_da_de_baja()
    {
        AgregarTelefono("token-vivo");
        AgregarTelefono("token-viejo");
        _notificador.Invalidos.Add("token-viejo");

        var resultado = await Enviar().Handle(new EnviarNotificacionDePruebaCommand(null, null), CancellationToken.None);

        Assert.Equal(1, resultado.Enviadas);
        Assert.Equal(1, resultado.TokensEliminados);
        Assert.Equal("token-vivo", Assert.Single(_dispositivos.Lista).Token);
        Assert.Equal(1, _unidad.Guardados);
    }

    [Fact]
    public async Task Usuario_sin_telefonos_da_no_encontrado()
    {
        await Assert.ThrowsAsync<NoEncontradoException>(() =>
            Enviar().Handle(new EnviarNotificacionDePruebaCommand(null, null), CancellationToken.None));
    }

    [Fact]
    public async Task Servidor_sin_Firebase_configurado_avisa_con_un_error_claro()
    {
        AgregarTelefono("token-1");
        _notificador.Configurado = false;

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Enviar().Handle(new EnviarNotificacionDePruebaCommand(null, null), CancellationToken.None));

        Assert.Contains("Firebase", ex.Message);
    }

    // ----- Dominio -----

    [Fact]
    public void Un_dispositivo_necesita_token()
    {
        Assert.Throws<ArgumentException>(() =>
            DispositivoMovil.Registrar(Guid.NewGuid(), Guid.NewGuid(), " ", "Android", Ahora));
    }

    private void AgregarTelefono(string token) =>
        _dispositivos.Lista.Add(DispositivoMovil.Registrar(
            _sesion.OperadorId!.Value, _sesion.UsuarioId!.Value, token, "Android", Ahora));

    // ----- Dobles de prueba -----

    private sealed class SesionFalsa : ITenantContext
    {
        public Guid? UsuarioId { get; set; } = Guid.NewGuid();
        public Guid? OperadorId { get; set; } = Guid.NewGuid();
        public Guid? ComercioId => null;
        public string? Perfil => Perfiles.Repartidor;
        public bool EstaAutenticado => UsuarioId is not null;
    }

    private sealed class DispositivosEnMemoria : IDispositivoMovilRepository
    {
        public List<DispositivoMovil> Lista { get; } = [];

        public Task<DispositivoMovil?> ObtenerPorTokenAsync(string token, CancellationToken ct) =>
            Task.FromResult(Lista.FirstOrDefault(d => d.Token == token));

        public Task<IReadOnlyList<DispositivoMovil>> ListarPorUsuarioAsync(Guid usuarioId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<DispositivoMovil>>(Lista.Where(d => d.UsuarioId == usuarioId).ToList());

        public Task AgregarAsync(DispositivoMovil dispositivo, CancellationToken ct)
        {
            Lista.Add(dispositivo);
            return Task.CompletedTask;
        }

        public void Eliminar(DispositivoMovil dispositivo) => Lista.Remove(dispositivo);
    }

    private sealed class NotificadorFalso : INotificadorPush
    {
        public bool Configurado { get; set; } = true;
        public HashSet<string> Invalidos { get; } = [];
        public List<(string Token, NotificacionPush Notificacion)> Enviados { get; } = [];

        public Task<ResultadoPush> EnviarAsync(string token, NotificacionPush notificacion, CancellationToken ct)
        {
            if (!Configurado)
                return Task.FromResult(ResultadoPush.NoConfigurado);

            if (Invalidos.Contains(token))
                return Task.FromResult(ResultadoPush.TokenInvalido);

            Enviados.Add((token, notificacion));
            return Task.FromResult(ResultadoPush.Enviado);
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
    }

    private sealed class RelojFijo(DateTimeOffset ahora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => ahora;
    }
}
