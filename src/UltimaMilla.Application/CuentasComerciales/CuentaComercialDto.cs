using UltimaMilla.Domain.Organizacion;

namespace UltimaMilla.Application.CuentasComerciales;

public sealed record CuentaComercialDto(
    Guid Id,
    Guid OperadorId,
    string OperadorNombre,
    Guid ComercioId,
    string ComercioNombre,
    bool Activa,
    EstadoCuentaComercial Estado);