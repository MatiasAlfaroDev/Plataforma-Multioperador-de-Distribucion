using Microsoft.AspNetCore.Identity;

namespace UltimaMilla.Infrastructure.Identidad;

public sealed class Usuario : IdentityUser<Guid>
{
    public string NombreCompleto { get; set; } = string.Empty;

    public bool Activo { get; set; } = true;
}