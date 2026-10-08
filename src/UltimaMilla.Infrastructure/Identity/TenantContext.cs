using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using UltimaMilla.Application.Abstractions;

namespace UltimaMilla.Infrastructure.Identity;

/// <summary>
/// Resuelve el tenant actual.
/// En una petición HTTP lo obtiene de los claims del usuario autenticado.
/// En procesos internos puede establecerse explícitamente mediante ITenantScope.
/// </summary>
public sealed class TenantContext :
    ITenantContext,
    ITenantScope
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    private Guid? _operadorIdInterno;
    private Guid? _comercioIdInterno;

    public TenantContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? OperadorId =>
        _operadorIdInterno
        ?? ObtenerGuidClaim("operador_id");

    public Guid? ComercioId =>
        _comercioIdInterno
        ?? ObtenerGuidClaim("comercio_id");

    public IDisposable UsarTenant(
        Guid operadorId,
        Guid? comercioId = null)
    {
        if (operadorId == Guid.Empty)
            throw new ArgumentException(
                "El operador es obligatorio.",
                nameof(operadorId));

        var operadorAnterior = _operadorIdInterno;
        var comercioAnterior = _comercioIdInterno;

        _operadorIdInterno = operadorId;
        _comercioIdInterno = comercioId;

        return new TenantScope(() =>
        {
            _operadorIdInterno = operadorAnterior;
            _comercioIdInterno = comercioAnterior;
        });
    }

    private Guid? ObtenerGuidClaim(string nombreClaim)
    {
        var valor = _httpContextAccessor.HttpContext?
            .User
            .FindFirstValue(nombreClaim);

        return Guid.TryParse(valor, out var id)
            ? id
            : null;
    }

    private sealed class TenantScope(Action alCerrar) : IDisposable
    {
        private bool _cerrado;

        public void Dispose()
        {
            if (_cerrado)
                return;

            _cerrado = true;
            alCerrar();
        }
    }
}