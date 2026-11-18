using UltimaMilla.Application.CuentasComerciales.Gestion;

namespace UltimaMilla.Application.Abstractions;

/// <summary>Lectura de los comercios vinculados a un operador, con sus datos de gestión.</summary>
public interface IComerciosDelOperadorLectura
{
    Task<IReadOnlyList<ComercioDelOperadorDto>> ListarAsync(
        Guid operadorId,
        CancellationToken ct);
}
