using System.Security.Claims;
using UltimaMilla.Application.Seguridad;
using System.Security.Cryptography;
using UltimaMilla.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace UltimaMilla.Api.Seguridad;

public sealed class HttpTenantContext :
    ITenantContext,
    IOperadorActivo,
    IContextoSeguimientoPublico
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

    public async Task<Guid?> ValidarTokenYSeleccionarOperadorAsync(
    string token,
    CancellationToken ct)
{
    if (EstaAutenticado)
        throw new UnauthorizedAccessException(
            "Esta operación está reservada al seguimiento público.");

    if (string.IsNullOrWhiteSpace(token) || token.Length != 64)
        return null;

    byte[] bytes;

    try
    {
        bytes = Convert.FromHexString(token);
    }
    catch (FormatException)
    {
        return null;
    }

    var hash = Convert.ToHexString(
        SHA256.HashData(bytes));

    // La consulta debe realizarse sin filtros porque todavía
    // no existe un operador seleccionado.
    var db = _httpContextAccessor.HttpContext!
        .RequestServices.GetRequiredService<AppDbContext>();

    var envio = await db.Envios
        .IgnoreQueryFilters()
        .AsNoTracking()
        .Where(e => e.TokenSeguimientoHash == hash)
        .Select(e => new { e.OperadorId })
        .FirstOrDefaultAsync(ct);

    if (envio is null)
        return null;

    _operadorSeleccionado = envio.OperadorId;

    return envio.OperadorId;
}
}