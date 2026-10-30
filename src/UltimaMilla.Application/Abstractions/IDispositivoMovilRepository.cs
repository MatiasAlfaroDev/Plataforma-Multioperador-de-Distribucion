using UltimaMilla.Domain.Notificaciones;

namespace UltimaMilla.Application.Abstractions;

/// <summary>Teléfonos registrados para recibir notificaciones push. Lo implementa Infrastructure con EF Core.</summary>
public interface IDispositivoMovilRepository
{
    /// <summary>El teléfono con ese token en el operador de la sesión, si ya estaba registrado.</summary>
    Task<DispositivoMovil?> ObtenerPorTokenAsync(string token, CancellationToken ct);

    /// <summary>Los teléfonos donde el usuario tiene la sesión abierta.</summary>
    Task<IReadOnlyList<DispositivoMovil>> ListarPorUsuarioAsync(Guid usuarioId, CancellationToken ct);

    Task AgregarAsync(DispositivoMovil dispositivo, CancellationToken ct);

    /// <summary>Se usa cuando Firebase avisa que el token ya no existe.</summary>
    void Eliminar(DispositivoMovil dispositivo);
}
