namespace UltimaMilla.Application.Retiros.Commands.CrearRetiro;

public sealed record CrearRetiroCommand(
    Guid CuentaComercialId,
    string Motivo,
    DireccionRetiroCommand DomicilioRetiro,
    DireccionRetiroCommand DomicilioDestino,
    DateOnly Fecha,
    TimeOnly HoraDesde,
    TimeOnly HoraHasta,
    IReadOnlyList<BultoRetiroCommand> Bultos);

public sealed record DireccionRetiroCommand(
    string Calle,
    string Numero,
    string Localidad,
    string Departamento);

public sealed record BultoRetiroCommand(
    string Identificacion,
    decimal PesoKg,
    int AltoCm,
    int AnchoCm,
    int ProfundidadCm);