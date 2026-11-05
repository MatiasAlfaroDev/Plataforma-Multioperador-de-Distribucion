using System;
using UltimaMilla.Application.Seguridad;

namespace UltimaMilla.Worker;

/// <summary>
/// Contexto de inquilino para el Worker.
/// En el Worker no hay usuario autenticado: el OperadorId viaja en cada mensaje.
/// El consumidor lo fija antes de llamar al Handler, permitiendo que los filtros por operador
/// y TenantSaveChangesInterceptor funcionen igual que en la API (informe 5.1).
/// </summary>
public sealed class TenantDelMensaje : ITenantContext
{
    public Guid? OperadorId { get; private set; }

    public Guid? UsuarioId => null;

    public Guid? ComercioId => null;

    public string? Perfil => null;

    public bool EstaAutenticado => OperadorId.HasValue;

    /// <summary>
    /// Fija el ID del operador para el mensaje actual.
    /// </summary>
    /// <param name="operadorId">ID del operador.</param>
    /// <exception cref="ArgumentException">Si el ID es Guid.Empty.</exception>
    public void Fijar(Guid operadorId)
    {
        if (operadorId == Guid.Empty)
            throw new ArgumentException("El mensaje no trae operador.", nameof(operadorId));

        OperadorId = operadorId;
    }
}
