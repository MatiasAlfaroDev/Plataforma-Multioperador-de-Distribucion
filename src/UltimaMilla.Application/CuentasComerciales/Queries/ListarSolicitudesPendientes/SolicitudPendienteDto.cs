namespace UltimaMilla.Application.CuentasComerciales
    .Queries.ListarSolicitudesPendientes;

public sealed record SolicitudPendienteDto(
    Guid Id,
    Guid ComercioId,
    string NombreComercio,
    DateTimeOffset CreadoEn
);