using UltimaMilla.Domain.Common;

namespace UltimaMilla.Domain.Rutas;

public sealed class ParadaRuta : AuditableEntity
{
    public Guid HojaDeRutaId { get; private set; }

    public Guid? EnvioId { get; private set; }
    public Guid? RetiroId { get; private set; }

    public int Orden { get; private set; }

    public DateTimeOffset? LiberadaEn { get; private set; }

    public bool Activa => LiberadaEn is null;

    public bool EsEntrega =>
        TipoOperacion is TipoOperacionRuta.Entrega
            or TipoOperacionRuta.DevolucionComercio
            or TipoOperacionRuta.EntregaRetiroComercio;

    public bool EsRetiro =>
        TipoOperacion == TipoOperacionRuta.Retiro;
    public TipoOperacionRuta TipoOperacion { get; private set; }
    private ParadaRuta() { }

    // Conserva la creación utilizada por las entregas existentes.
    internal static ParadaRuta Crear(
        Guid hojaDeRutaId,
        Guid envioId,
        int orden)
    {
        ValidarDatos(hojaDeRutaId, envioId, orden);

        return new ParadaRuta
        {
            Id = Guid.NewGuid(),
            HojaDeRutaId = hojaDeRutaId,
            EnvioId = envioId,
            RetiroId = null,
            Orden = orden,
            TipoOperacion = TipoOperacionRuta.Entrega,
        };
    }

    internal static ParadaRuta CrearRetiro(
        Guid hojaDeRutaId,
        Guid retiroId,
        int orden)
    {
        ValidarDatos(hojaDeRutaId, retiroId, orden);

        return new ParadaRuta
        {
            Id = Guid.NewGuid(),
            HojaDeRutaId = hojaDeRutaId,
            EnvioId = null,
            RetiroId = retiroId,
            Orden = orden,
            TipoOperacion = TipoOperacionRuta.Retiro
        };
    }

    internal static ParadaRuta CrearDevolucionComercio(
        Guid hojaDeRutaId,
        Guid envioId,
        int orden)
    {
        ValidarDatos(hojaDeRutaId, envioId, orden);

        return new ParadaRuta
        {
            Id = Guid.NewGuid(),
            HojaDeRutaId = hojaDeRutaId,
            EnvioId = envioId,
            RetiroId = null,
            Orden = orden,
            TipoOperacion = TipoOperacionRuta.DevolucionComercio
        };
    }

    internal static ParadaRuta CrearEntregaRetiroComercio(
        Guid hojaDeRutaId,
        Guid retiroId,
        int orden)
    {
        ValidarDatos(hojaDeRutaId, retiroId, orden);

        return new ParadaRuta
        {
            Id = Guid.NewGuid(),
            HojaDeRutaId = hojaDeRutaId,
            EnvioId = null,
            RetiroId = retiroId,
            Orden = orden,
            TipoOperacion = TipoOperacionRuta.EntregaRetiroComercio
        };
    }

    private static void ValidarDatos(
        Guid hojaDeRutaId,
        Guid referenciaId,
        int orden)
    {
        if (hojaDeRutaId == Guid.Empty ||
            referenciaId == Guid.Empty)
        {
            throw new ArgumentException(
                "La hoja de ruta y la referencia son obligatorias.");
        }

        if (orden <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(orden));
        }
    }

    internal void Liberar(DateTimeOffset fecha)
    {
        if (!Activa)
        {
            throw new InvalidOperationException(
                "La parada ya fue liberada.");
        }

        LiberadaEn = fecha.ToUniversalTime();
    }

    internal void CambiarOrden(int nuevoOrden)
    {
        if (!Activa)
        {
            throw new InvalidOperationException(
                "No se puede reordenar una parada histórica.");
        }

        if (nuevoOrden <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(nuevoOrden));
        }

        Orden = nuevoOrden;
    }
}