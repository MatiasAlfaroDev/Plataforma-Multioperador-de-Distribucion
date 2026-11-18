
using UltimaMilla.Application.Seguridad;

namespace UltimaMilla.IntegrationTests.Seguridad;

public sealed class TestTenantContext : ITenantContext
{
    public Guid? UsuarioId { get; set; } = Guid.NewGuid();
    public Guid? OperadorId { get; set; }
    public Guid? ComercioId { get; set; }
    public string? Perfil { get; set; } = "AdministradorOperador";
    public bool EstaAutenticado { get; set; } = true;
}
