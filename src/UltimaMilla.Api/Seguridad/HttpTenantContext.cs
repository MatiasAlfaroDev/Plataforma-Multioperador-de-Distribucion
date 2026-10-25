
using System.Security.Claims;
using UltimaMilla.Application.Seguridad;

namespace UltimaMilla.Api.Seguridad;

public sealed class HttpTenantContext : ITenantContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpTenantContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? Usuario =>
        _httpContextAccessor.HttpContext?.User;

    public bool EstaAutenticado =>
        Usuario?.Identity?.IsAuthenticated == true;

    public Guid? UsuarioId =>
        ObtenerGuid(ClaimTypes.NameIdentifier);

    public Guid? OperadorId =>
        ObtenerGuid("operador_id");

    public Guid? ComercioId =>
        ObtenerGuid("cuenta_comercial_id");

    private Guid? ObtenerGuid(string tipoClaim)
    {
        if (!EstaAutenticado)
            return null;

        var valor = Usuario?.FindFirstValue(tipoClaim);

        return Guid.TryParse(valor, out var id)
            ? id
            : null;
    }

    public string? Perfil =>
    EstaAutenticado
        ? Usuario?.FindFirstValue("perfil")
        : null; 
}
