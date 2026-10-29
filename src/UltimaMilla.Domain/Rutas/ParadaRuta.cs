
using UltimaMilla.Domain.Common;

namespace UltimaMilla.Domain.Rutas;

public sealed class ParadaRuta : AuditableEntity
{
    public Guid HojaDeRutaId { get; private set; }
    public Guid EnvioId { get; private set; }
    public int Orden { get; private set; }

    // Null significa que la asignación sigue activa.
    public DateTimeOffset? LiberadaEn { get; private set; }

    public bool Activa => LiberadaEn is null;

    private ParadaRuta() { }

    internal static ParadaRuta Crear(
        Guid hojaDeRutaId,
        Guid envioId,
        int orden)
    {
        if (hojaDeRutaId == Guid.Empty || envioId == Guid.Empty)
            throw new ArgumentException(
                "La hoja de ruta y el envío son obligatorios.");

        if (orden <= 0)
            throw new ArgumentOutOfRangeException(nameof(orden));

        return new ParadaRuta
        {
            Id = Guid.NewGuid(),
            HojaDeRutaId = hojaDeRutaId,
            EnvioId = envioId,
            Orden = orden
        };
    }

    internal void Liberar(DateTimeOffset fecha)
    {
        if (!Activa)
            throw new InvalidOperationException(
                "La parada ya fue liberada.");

        LiberadaEn = fecha;
    }

    internal void CambiarOrden(int nuevoOrden)
    {
        if (!Activa)
            throw new InvalidOperationException(
                "No se puede reordenar una parada histórica.");

        if (nuevoOrden <= 0)
            throw new ArgumentOutOfRangeException(nameof(nuevoOrden));

        Orden = nuevoOrden;
    }
}
