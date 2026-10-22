namespace UltimaMilla.Domain.Envios;

public sealed class PruebaEntrega
{
    public Guid Id { get; private set; }
    public string NombreReceptor { get; private set; } = string.Empty;
    public string? DocumentoReceptor { get; private set; }
    public string? ReferenciaFirma { get; private set; }
    public string? ReferenciaFotografia { get; private set; }
    public DateTimeOffset OcurridoEn { get; private set; }
    public decimal Latitud { get; private set; }
    public decimal Longitud { get; private set; }

    private PruebaEntrega() { }

    public static PruebaEntrega Crear(
        string nombreReceptor,
        string? documentoReceptor,
        string? referenciaFirma,
        string? referenciaFotografia,
        DateTimeOffset ocurridoEn,
        decimal latitud,
        decimal longitud)
    {
        if (string.IsNullOrWhiteSpace(nombreReceptor))
            throw new ArgumentException("El nombre del receptor es obligatorio.");

        if (latitud < -90 || latitud > 90 || longitud < -180 || longitud > 180)
            throw new ArgumentException("Las coordenadas no son válidas.");

        return new PruebaEntrega
        {
            Id = Guid.NewGuid(),
            NombreReceptor = nombreReceptor.Trim(),
            DocumentoReceptor = documentoReceptor?.Trim(),
            ReferenciaFirma = referenciaFirma?.Trim(),
            ReferenciaFotografia = referenciaFotografia?.Trim(),
            OcurridoEn = ocurridoEn,
            Latitud = latitud,
            Longitud = longitud
        };
    }
}
