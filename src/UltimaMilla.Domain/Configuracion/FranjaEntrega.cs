
using UltimaMilla.Domain.Common;

namespace UltimaMilla.Domain.Configuracion;

public class FranjaEntrega : AuditableEntity
{
    public Guid ZonaCoberturaId { get; private set; }
    public TimeOnly HoraDesde { get; private set; }
    public TimeOnly HoraHasta { get; private set; }

    private FranjaEntrega() { }

    internal static FranjaEntrega Crear(
        Guid zonaCoberturaId,
        TimeOnly desde,
        TimeOnly hasta)
    {
        if (zonaCoberturaId == Guid.Empty)
            throw new ArgumentException("La zona es obligatoria.");

        if (desde >= hasta)
            throw new ArgumentException("La franja horaria es inválida.");

        return new FranjaEntrega
        {
            Id = Guid.NewGuid(),
            ZonaCoberturaId = zonaCoberturaId,
            HoraDesde = desde,
            HoraHasta = hasta
        };
    }
}
