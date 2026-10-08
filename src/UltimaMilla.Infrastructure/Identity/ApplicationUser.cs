using Microsoft.AspNetCore.Identity;

namespace UltimaMilla.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string NombreCompleto { get; set; } = string.Empty;

    public bool Activo { get; set; } = true;

    public ICollection<UsuarioOperador> Operadores { get; set; }
        = new List<UsuarioOperador>();

    public ICollection<UsuarioComercio> Comercios { get; set; }
        = new List<UsuarioComercio>();
}