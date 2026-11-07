using UltimaMilla.Domain.Common;

namespace UltimaMilla.Domain.Retiros;

public sealed class BultoRetiro : AuditableEntity
{
    public string Identificacion { get; private set; } = string.Empty;
    public decimal PesoKg { get; private set; }
    public int AltoCm { get; private set; }
    public int AnchoCm { get; private set; }
    public int ProfundidadCm { get; private set; }

    public decimal VolumenMetrosCubicos =>
        (decimal)AltoCm * AnchoCm * ProfundidadCm / 1_000_000m;

    private BultoRetiro() { }

    public static BultoRetiro Crear(
        string identificacion,
        decimal pesoKg,
        int altoCm,
        int anchoCm,
        int profundidadCm)
    {
        if (string.IsNullOrWhiteSpace(identificacion))
            throw new ArgumentException(
                "La identificación del bulto es obligatoria.");

        if (identificacion.Trim().Length > 100)
            throw new ArgumentException(
                "La identificación no puede superar los 100 caracteres.");

        if (pesoKg <= 0 || pesoKg > 1000)
            throw new ArgumentOutOfRangeException(nameof(pesoKg));

        if (altoCm is < 1 or > 500 ||
            anchoCm is < 1 or > 500 ||
            profundidadCm is < 1 or > 500)
            throw new ArgumentException(
                "Las dimensiones del bulto no son válidas.");

        return new BultoRetiro
        {
            Id = Guid.NewGuid(),
            Identificacion = identificacion.Trim(),
            PesoKg = pesoKg,
            AltoCm = altoCm,
            AnchoCm = anchoCm,
            ProfundidadCm = profundidadCm
        };
    }
}