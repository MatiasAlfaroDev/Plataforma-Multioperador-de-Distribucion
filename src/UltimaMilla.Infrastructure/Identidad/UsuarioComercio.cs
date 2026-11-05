
namespace UltimaMilla.Infrastructure.Identidad;

public sealed class UsuarioComercio
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UsuarioId { get; set; }

    public Guid ComercioId { get; set; }

    public string Perfil { get; set; } = string.Empty;

    public bool Activa { get; set; } = true;

    public Usuario Usuario { get; set; } = null!;
}
