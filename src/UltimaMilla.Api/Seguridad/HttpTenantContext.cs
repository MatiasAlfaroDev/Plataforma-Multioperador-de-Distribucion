using System.Security.Claims;
using UltimaMilla.Application.Seguridad;

namespace UltimaMilla.Api.Seguridad;

public sealed class HttpTenantContext : ITenantContext,  IOperadorActivo
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    private Guid? _operadorSeleccionado;

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
        ObtenerGuid("operador_id") ?? _operadorSeleccionado;

    public Guid? ComercioId =>
        ObtenerGuid("comercio_id");

    public string? Perfil =>
        EstaAutenticado
            ? Usuario?.FindFirstValue("perfil")
                ?? Usuario?.FindFirstValue("perfil_comercio")
            : null;

    public void SeleccionarOperador(Guid operadorId)
    {
        if (!EstaAutenticado || !ComercioId.HasValue)
            throw new UnauthorizedAccessException();

        _operadorSeleccionado = operadorId;
    }

    private Guid? ObtenerGuid(string tipoClaim)
    {
        if (!EstaAutenticado)
            return null;

        var valor = Usuario?.FindFirstValue(tipoClaim);

        return Guid.TryParse(valor, out var id)
            ? id
            : null;
    }
}