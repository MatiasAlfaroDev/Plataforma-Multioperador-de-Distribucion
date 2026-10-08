namespace UltimaMilla.Infrastructure.Identity;

public sealed class UsuarioOperador
{
    public Guid UsuarioId { get; set; }

    public ApplicationUser Usuario { get; set; } = null!;

    public Guid OperadorId { get; set; }

    public string Rol { get; set; } = string.Empty;
}