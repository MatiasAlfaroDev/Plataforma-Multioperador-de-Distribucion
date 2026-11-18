using System.Text.Json.Serialization;
using UltimaMilla.Domain.Organizacion;

namespace UltimaMilla.Application.CuentasComerciales.Gestion;

public sealed record ComercioDelOperadorDto(
    Guid CuentaId,
    Guid ComercioId,
    string Nombre,
    string Rut,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] EstadoCuentaComercial Estado,
    bool Activa,
    string? Calle,
    string? Numero,
    string? Localidad,
    string? Departamento);
