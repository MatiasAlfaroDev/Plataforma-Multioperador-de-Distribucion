namespace UltimaMilla.Application.Seguridad;

public interface ITenantContext
{
    Guid? UsuarioId { get; }
    Guid? OperadorId { get; }
    Guid? ComercioId { get; }

    bool EstaAutenticado { get; }
}