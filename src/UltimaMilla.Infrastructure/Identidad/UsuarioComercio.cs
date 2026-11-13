
namespace UltimaMilla.Infrastructure.Identidad;

public sealed class UsuarioComercio
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UsuarioId { get; set; }

    public Guid ComercioId { get; set; }

    public string Perfil { get; set; } = string.Empty;

    public bool Activa { get; set; } = true;

    /// <summary>RF 37: hash SHA-256 de la clave de API con la que el sistema del comercio llama a la API.</summary>
    public string? ClaveApiHash { get; set; }

    public Usuario Usuario { get; set; } = null!;
}
