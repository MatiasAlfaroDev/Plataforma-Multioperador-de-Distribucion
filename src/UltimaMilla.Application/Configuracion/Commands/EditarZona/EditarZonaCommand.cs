using UltimaMilla.Application.Configuracion.Commands.CrearZona;

namespace UltimaMilla.Application.Configuracion.Commands.EditarZona;

/// <summary>
/// Editar una zona de cobertura existente. Las coberturas y las franjas que llegan
/// reemplazan a las anteriores (se usan los mismos records que en el alta).
/// </summary>
public sealed record EditarZonaCommand(
    Guid OperadorId,
    Guid ZonaId,
    string Nombre,
    string Departamento,
    int MinutosTraslado,
    IReadOnlyList<CoberturaCommand> Coberturas,
    IReadOnlyList<FranjaEntregaCommand> Franjas);
