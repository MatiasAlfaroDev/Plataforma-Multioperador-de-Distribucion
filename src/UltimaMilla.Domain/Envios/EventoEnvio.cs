namespace UltimaMilla.Domain.Envios;

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
    public decimal? Latitud { get; private set; }
    public decimal? Longitud { get; private set; }

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
        decimal? longitud = null)
    {
        if (operadorId == Guid.Empty || envioId == Guid.Empty)
            throw new ArgumentException("El operador y el envío son obligatorios.");

        if (string.IsNullOrWhiteSpace(origen) ||
            string.IsNullOrWhiteSpace(responsable))
            throw new ArgumentException("El origen y el responsable son obligatorios.");

        if (latitud.HasValue != longitud.HasValue)
            throw new ArgumentException("La ubicación debe incluir latitud y longitud.");

        if (latitud is < -90 or > 90 || longitud is < -180 or > 180)
            throw new ArgumentException("Las coordenadas no son válidas.");

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
            Longitud = longitud
        };
    }
}
