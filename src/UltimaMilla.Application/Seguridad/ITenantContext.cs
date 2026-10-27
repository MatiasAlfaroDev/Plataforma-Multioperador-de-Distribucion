namespace UltimaMilla.Application.Seguridad;

public interface ITenantContext
{
    Guid? UsuarioId { get; }
    Guid? OperadorId { get; }
    Guid? ComercioId { get; }
    string? Perfil { get; }
    bool EstaAutenticado { get; }
}

