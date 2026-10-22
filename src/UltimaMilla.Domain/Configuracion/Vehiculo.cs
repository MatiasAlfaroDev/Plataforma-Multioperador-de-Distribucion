
using UltimaMilla.Domain.Common;

namespace UltimaMilla.Domain.Organizacion;

public sealed class Vehiculo : AuditableEntity, ITenantEntity
{
    public Guid OperadorId { get; private set; }
    public string Matricula { get; private set; } = string.Empty;

    public decimal CapacidadPesoKg { get; private set; }
    public decimal CapacidadVolumenM3 { get; private set; }

    public bool Activo { get; private set; }

    private Vehiculo() { }

    public static Vehiculo Crear(
        Guid operadorId,
        string matricula,
        decimal capacidadPesoKg,
        decimal capacidadVolumenM3)
    {
        if (operadorId == Guid.Empty)
            throw new ArgumentException("El operador es obligatorio.");

        if (string.IsNullOrWhiteSpace(matricula))
            throw new ArgumentException("La matrícula es obligatoria.");

        if (capacidadPesoKg <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacidadPesoKg));

        if (capacidadVolumenM3 <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacidadVolumenM3));

        return new Vehiculo
        {
            Id = Guid.NewGuid(),
            OperadorId = operadorId,
            Matricula = matricula.Trim().ToUpperInvariant(),
            CapacidadPesoKg = capacidadPesoKg,
            CapacidadVolumenM3 = capacidadVolumenM3,
            Activo = true
        };
    }

    public void Desactivar() => Activo = false;

    public void Activar() => Activo = true;
}
