namespace UltimaMilla.Application.Envios.Commands.CrearEnvio;

/// <summary>Orden de dar de alta un envío (cambia el estado del sistema).</summary>
public sealed record CrearEnvioCommand(
    Guid CuentaComercialId,
    string ReferenciaExterna,
    string Modalidad,
    string DestinatarioNombre,
    string? DestinatarioTelefono,
    string Calle,
    string Numero,
    string Localidad,
    string Departamento,
    IReadOnlyList<BultoCommand> Bultos);

public sealed record BultoCommand(decimal PesoKg, int AltoCm, int AnchoCm, int ProfundidadCm);
