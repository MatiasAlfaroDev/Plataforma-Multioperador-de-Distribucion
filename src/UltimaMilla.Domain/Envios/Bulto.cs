namespace UltimaMilla.Domain.Envios;

/// <summary>Cada paquete físico de un envío, con su identificación, peso y dimensiones (RF 8).</summary>
public class Bulto
{
    public Guid Id { get; private set; }
    public string Identificacion { get; private set; } = string.Empty;
    public decimal PesoKg { get; private set; }
    public int AltoCm { get; private set; }
    public int AnchoCm { get; private set; }
    public int ProfundidadCm { get; private set; }
    public decimal VolumenMetrosCubicos =>
    (decimal)AltoCm * AnchoCm * ProfundidadCm / 1_000_000m;

    private Bulto() { }

    internal static Bulto Crear(string identificacion, NuevoBulto datos)
    {
        if (datos.PesoKg <= 0)
            throw new ArgumentException("El peso del bulto debe ser mayor que cero.", nameof(datos));
        if (datos.AltoCm <= 0 || datos.AnchoCm <= 0 || datos.ProfundidadCm <= 0)
            throw new ArgumentException("Las dimensiones del bulto deben ser mayores que cero.", nameof(datos));

        return new Bulto
        {
            Id = Guid.NewGuid(),
            Identificacion = identificacion,
            PesoKg = datos.PesoKg,
            AltoCm = datos.AltoCm,
            AnchoCm = datos.AnchoCm,
            ProfundidadCm = datos.ProfundidadCm
        };
    }
}
