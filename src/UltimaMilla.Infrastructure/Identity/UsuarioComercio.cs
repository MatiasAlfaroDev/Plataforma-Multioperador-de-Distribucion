namespace UltimaMilla.Infrastructure.Identity;

public sealed class UsuarioComercio
{
    public Guid UsuarioId { get; set; }

    public ApplicationUser Usuario { get; set; } = null!;

    public Guid ComercioId { get; set; }

    public string Rol { get; set; } = "COMERCIO";
}