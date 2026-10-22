
using UltimaMilla.Domain.Common;
using UltimaMilla.Domain.Envios;
using UltimaMilla.Domain.Organizacion;

namespace UltimaMilla.Domain.Rutas;

public sealed class HojaDeRuta : AuditableEntity, ITenantEntity
{
    private readonly List<ParadaRuta> _paradas = [];

    public Guid OperadorId { get; private set; }
    public Guid RepartidorId { get; private set; }
    public Guid VehiculoId { get; private set; }
    public DateOnly Fecha { get; private set; }
    public TimeOnly? HoraSalida { get; private set; }
    public int MaximoParadas { get; private set; }
    public decimal CapacidadPesoKg { get; private set; }
    public decimal CapacidadVolumenM3 { get; private set; }

    public decimal PesoAsignadoKg { get; private set; }
    public decimal VolumenAsignadoM3 { get; private set; }

    public IReadOnlyCollection<ParadaRuta> Paradas => _paradas;

    private HojaDeRuta() { }

    
    public static HojaDeRuta Crear(
        Guid operadorId,
        Repartidor repartidor,
        Vehiculo vehiculo,
        DateOnly fecha,
        int maximoParadas)
    {
        ArgumentNullException.ThrowIfNull(repartidor);
        ArgumentNullException.ThrowIfNull(vehiculo);

        if (operadorId == Guid.Empty)
            throw new ArgumentException("El operador es obligatorio.");

        if (repartidor.OperadorId != operadorId)
            throw new InvalidOperationException(
                "El repartidor pertenece a otro operador.");

        if (vehiculo.OperadorId != operadorId)
            throw new InvalidOperationException(
                "El vehículo pertenece a otro operador.");

        if (!repartidor.Activo)
            throw new InvalidOperationException(
                "El repartidor está inactivo.");

        if (!vehiculo.Activo)
            throw new InvalidOperationException(
                "El vehículo está inactivo.");

        if (maximoParadas <= 0)
            throw new ArgumentOutOfRangeException(nameof(maximoParadas));

        return new HojaDeRuta
        {
            Id = Guid.NewGuid(),
            OperadorId = operadorId,
            RepartidorId = repartidor.Id,
            VehiculoId = vehiculo.Id,
            Fecha = fecha,
            MaximoParadas = maximoParadas,
            CapacidadPesoKg = vehiculo.CapacidadPesoKg,
            CapacidadVolumenM3 = vehiculo.CapacidadVolumenM3
        };
    }

    public void AgregarEnvio(Envio envio)
    {
        ArgumentNullException.ThrowIfNull(envio);

        if (envio.OperadorId != OperadorId)
            throw new InvalidOperationException(
                "El envío pertenece a otro operador.");

        if (envio.Estado != EstadoEnvio.EnDeposito &&
            envio.Estado != EstadoEnvio.Reprogramado)
            throw new InvalidOperationException(
                "El envío no está disponible para asignación.");

        if (envio.Estado == EstadoEnvio.Reprogramado)
        {
            if (!envio.FechaReprogramada.HasValue ||
                DateOnly.FromDateTime(
                    envio.FechaReprogramada.Value.Date) != Fecha)
            {
                throw new InvalidOperationException(
                    "La fecha de la ruta no coincide con la reprogramación.");
            }
        }
        else if (envio.FechaEntregaComprometida.HasValue &&
                envio.FechaEntregaComprometida.Value != Fecha)
        {
            throw new InvalidOperationException(
                "La fecha de la ruta no coincide con la fecha comprometida.");
        }

        if (_paradas.Any(p => p.EnvioId == envio.Id && p.Activa))
            throw new InvalidOperationException(
                "El envío ya está incluido en esta hoja de ruta.");

        if (_paradas.Count(p => p.Activa) >= MaximoParadas)
            throw new InvalidOperationException(
                "Se alcanzó el máximo de paradas.");

        if (PesoAsignadoKg + envio.PesoTotalKg > CapacidadPesoKg)
            throw new InvalidOperationException(
                "La carga supera la capacidad de peso.");

        if (VolumenAsignadoM3 +
            envio.VolumenTotalMetrosCubicos > CapacidadVolumenM3)
            throw new InvalidOperationException(
                "La carga supera la capacidad de volumen.");

        var siguienteOrden = _paradas.Count == 0
            ? 1
            : _paradas.Max(p => p.Orden) + 1;

        var parada = ParadaRuta.Crear(
            Id,
            envio.Id,
            siguienteOrden);

                _paradas.Add(parada);

        PesoAsignadoKg += envio.PesoTotalKg;
        VolumenAsignadoM3 += envio.VolumenTotalMetrosCubicos;
    }

    public void QuitarEnvio(Envio envio, DateTimeOffset fecha)
    {
        ArgumentNullException.ThrowIfNull(envio);

        var parada = _paradas.SingleOrDefault(
            p => p.EnvioId == envio.Id && p.Activa);

        if (parada is null)
            throw new InvalidOperationException(
                "El envío no tiene una asignación activa en esta ruta.");

        if (envio.OperadorId != OperadorId)
            throw new InvalidOperationException(
                "El envío pertenece a otro operador.");

        if (envio.Estado != EstadoEnvio.AsignadoARuta)
            throw new InvalidOperationException(
                "Solo se puede quitar un envío asignado a ruta.");

        parada.Liberar(fecha);

        PesoAsignadoKg -= envio.PesoTotalKg;
        VolumenAsignadoM3 -= envio.VolumenTotalMetrosCubicos;
    }

    public void ConfigurarHoraSalida(TimeOnly horaSalida)
    {
        HoraSalida = horaSalida;
    }
}
