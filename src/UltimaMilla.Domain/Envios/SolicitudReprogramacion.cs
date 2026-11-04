using UltimaMilla.Domain.Common;

namespace UltimaMilla.Domain.Envios;

public sealed class SolicitudReprogramacion : ITenantEntity
{
    public Guid Id { get; private set; }
    public Guid OperadorId { get; private set; }
    public Guid EnvioId { get; private set; }

    public DateTimeOffset SolicitadaEn { get; private set; }

    public EstadoSolicitudReprogramacion Estado { get; private set; }

    public DateTimeOffset? AtendidaEn { get; private set; }

    private SolicitudReprogramacion() { }

    public SolicitudReprogramacion(
        Guid operadorId,
        Guid envioId,
        DateTimeOffset solicitadaEn)
    {
        if (operadorId == Guid.Empty)
            throw new ArgumentException("El operador es obligatorio.");

        if (envioId == Guid.Empty)
            throw new ArgumentException("El envío es obligatorio.");

        Id = Guid.NewGuid();
        OperadorId = operadorId;
        EnvioId = envioId;
        SolicitadaEn = solicitadaEn;
        Estado = EstadoSolicitudReprogramacion.Pendiente;
    }

    public void MarcarAtendida(DateTimeOffset fecha)
    {
        if (Estado != EstadoSolicitudReprogramacion.Pendiente)
            throw new InvalidOperationException(
                "La solicitud ya fue procesada.");

        Estado = EstadoSolicitudReprogramacion.Atendida;
        AtendidaEn = fecha;
    }

    public void Rechazar(DateTimeOffset fecha)
    {
        if (Estado != EstadoSolicitudReprogramacion.Pendiente)
            throw new InvalidOperationException(
                "La solicitud ya fue procesada.");

        Estado = EstadoSolicitudReprogramacion.Rechazada;
        AtendidaEn = fecha;
    }
}