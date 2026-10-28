using UltimaMilla.Domain.Common;

namespace UltimaMilla.Domain.Notificaciones;

/// <summary>
/// Un teléfono con la app instalada que puede recibir notificaciones push (sección 6.11 y opcional 7.3).
/// Firebase le da a cada instalación un token: es la "dirección" a la que el servidor manda el aviso.
/// </summary>
/// <remarks>
/// Ciclo de vida del token: se registra al iniciar sesión, se renueva cuando el mismo teléfono
/// vuelve a registrarse (o inicia sesión otra persona) y se borra cuando Firebase responde que
/// ya no existe (la app se desinstaló o se borraron sus datos).
/// </remarks>
public sealed class DispositivoMovil : AuditableEntity, ITenantEntity
{
    /// <summary>Los tokens de Firebase andan por los 160–200 caracteres; se deja margen.</summary>
    public const int LargoMaximoToken = 512;

    public Guid OperadorId { get; private set; }

    /// <summary>Quién inició sesión por última vez en ese teléfono: a esa persona le llegan los avisos.</summary>
    public Guid UsuarioId { get; private set; }

    public string Token { get; private set; } = string.Empty;

    /// <summary>"Android" o "iOS".</summary>
    public string Plataforma { get; private set; } = string.Empty;

    /// <summary>La última vez que la app confirmó el token.</summary>
    public DateTimeOffset UltimoRegistroEn { get; private set; }

    // Constructor vacío que necesita EF Core.
    private DispositivoMovil() { }

    public static DispositivoMovil Registrar(
        Guid operadorId,
        Guid usuarioId,
        string token,
        string plataforma,
        DateTimeOffset ahora)
    {
        if (operadorId == Guid.Empty)
            throw new ArgumentException("El operador es obligatorio.", nameof(operadorId));

        if (usuarioId == Guid.Empty)
            throw new ArgumentException("El usuario es obligatorio.", nameof(usuarioId));

        if (string.IsNullOrWhiteSpace(token))
            throw new ArgumentException("El token del dispositivo es obligatorio.", nameof(token));

        if (token.Trim().Length > LargoMaximoToken)
            throw new ArgumentException("El token del dispositivo es demasiado largo.", nameof(token));

        if (string.IsNullOrWhiteSpace(plataforma))
            throw new ArgumentException("La plataforma es obligatoria.", nameof(plataforma));

        return new DispositivoMovil
        {
            Id = Guid.NewGuid(),
            OperadorId = operadorId,
            UsuarioId = usuarioId,
            Token = token.Trim(),
            Plataforma = plataforma.Trim(),
            UltimoRegistroEn = ahora
        };
    }

    /// <summary>
    /// El mismo teléfono volvió a registrarse. Si ahora inició sesión otra persona,
    /// los avisos pasan a ser para ella (un teléfono, un repartidor).
    /// </summary>
    public void Renovar(Guid usuarioId, DateTimeOffset ahora)
    {
        if (usuarioId == Guid.Empty)
            throw new ArgumentException("El usuario es obligatorio.", nameof(usuarioId));

        UsuarioId = usuarioId;
        UltimoRegistroEn = ahora;
    }
}
