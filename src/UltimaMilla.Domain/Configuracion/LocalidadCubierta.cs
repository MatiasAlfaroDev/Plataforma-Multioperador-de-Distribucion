
using UltimaMilla.Domain.Common;

namespace UltimaMilla.Domain.Configuracion;

public class LocalidadCubierta : AuditableEntity
{
    public Guid ZonaCoberturaId { get; private set; }

    public string Nombre { get; private set; } = string.Empty;

    public string? Sector { get; private set; }

    private LocalidadCubierta() { }

    internal static LocalidadCubierta Crear(
        Guid zonaCoberturaId,
        string nombre,
        string? sector)
    {
        if (zonaCoberturaId == Guid.Empty)
            throw new ArgumentException("La zona es obligatoria.");

        if (string.IsNullOrWhiteSpace(nombre))
            throw new ArgumentException("La localidad es obligatoria.");

        return new LocalidadCubierta
        {
            Id = Guid.NewGuid(),
            ZonaCoberturaId = zonaCoberturaId,
            Nombre = nombre.Trim(),
            Sector = string.IsNullOrWhiteSpace(sector)
                ? null
                : sector.Trim()
        };
    }
}
