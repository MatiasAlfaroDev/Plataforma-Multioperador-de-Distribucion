
namespace UltimaMilla.Domain.Envios;

public enum ModalidadDevolucion
{
    EntregaDirecta,
    RetiroEnDeposito,
    TransporteExterno
}

public sealed class EventoEnvio
{
    public Guid Id { get; private set; }
    public Guid OperadorId { get; private set; }
    public Guid EnvioId { get; private set; }
    public EstadoEnvio EstadoAnterior { get; private set; }
    public EstadoEnvio EstadoNuevo { get; private set; }
    public DisparadorEnvio Disparador { get; private set; }
    public DateTimeOffset OcurridoEn { get; private set; }
    public string Origen { get; private set; } = string.Empty;
    public string Responsable { get; private set; } = string.Empty;
    public string? Justificacion { get; private set; }
    public decimal? Latitud { get; private set; }
    public decimal? Longitud { get; private set; }

    public ModalidadDevolucion? ModalidadDevolucion { get; private set; }
    public string? ReceptorDevolucion { get; private set; }
    public string? ReferenciaConstanciaDevolucion { get; private set; }

    private EventoEnvio() { }

    public static EventoEnvio Crear(
        Guid operadorId,
        Guid envioId,
        EstadoEnvio estadoAnterior,
        EstadoEnvio estadoNuevo,
        DisparadorEnvio disparador,
        DateTimeOffset ocurridoEn,
        string origen,
        string responsable,
        decimal? latitud = null,
        decimal? longitud = null,
        string? justificacion = null,
        ModalidadDevolucion? modalidadDevolucion = null,
        string? receptorDevolucion = null,
        string? referenciaConstanciaDevolucion = null)
    {
        if (operadorId == Guid.Empty || envioId == Guid.Empty)
            throw new ArgumentException(
                "El operador y el envío son obligatorios.");

        if (string.IsNullOrWhiteSpace(origen) ||
            string.IsNullOrWhiteSpace(responsable))
            throw new ArgumentException(
                "El origen y el responsable son obligatorios.");

        if (latitud.HasValue != longitud.HasValue)
            throw new ArgumentException(
                "La ubicación debe incluir latitud y longitud.");

        if (latitud is < -90 or > 90 ||
            longitud is < -180 or > 180)
            throw new ArgumentException(
                "Las coordenadas no son válidas.");

        if (disparador == DisparadorEnvio.ConfirmarDevolucion)
        {
            if (!modalidadDevolucion.HasValue ||
                !Enum.IsDefined(modalidadDevolucion.Value))
                throw new ArgumentException(
                    "Debe indicar una modalidad de devolución válida.");

            if (string.IsNullOrWhiteSpace(receptorDevolucion))
                throw new ArgumentException(
                    "Debe identificar quién recibió la devolución.");

            if (string.IsNullOrWhiteSpace(referenciaConstanciaDevolucion))
                throw new ArgumentException(
                    "La devolución requiere una constancia de recepción.");

            if (receptorDevolucion.Trim().Length > 200 ||
                referenciaConstanciaDevolucion.Trim().Length > 500)
                throw new ArgumentException(
                    "Los datos de recepción superan la longitud permitida.");
        }
        else if (modalidadDevolucion.HasValue ||
                 receptorDevolucion is not null ||
                 referenciaConstanciaDevolucion is not null)
        {
            throw new ArgumentException(
                "Los datos de recepción solo corresponden a una devolución confirmada.");
        }

        return new EventoEnvio
        {
            Id = Guid.NewGuid(),
            OperadorId = operadorId,
            EnvioId = envioId,
            EstadoAnterior = estadoAnterior,
            EstadoNuevo = estadoNuevo,
            Disparador = disparador,
            OcurridoEn = ocurridoEn,
            Origen = origen.Trim(),
            Responsable = responsable.Trim(),
            Latitud = latitud,
            Longitud = longitud,
            Justificacion = string.IsNullOrWhiteSpace(justificacion)
                ? null
                : justificacion.Trim(),
            ModalidadDevolucion = modalidadDevolucion,
            ReceptorDevolucion = receptorDevolucion?.Trim(),
            ReferenciaConstanciaDevolucion =
                referenciaConstanciaDevolucion?.Trim()
        };
    }
}
