using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UltimaMilla.Domain.Reportes;

namespace UltimaMilla.Infrastructure.Persistence.Configurations;

/// <summary>Cierres diarios de la operación (letra 4): uno por operador y fecha.</summary>
public sealed class CierreDiarioConfiguration : IEntityTypeConfiguration<CierreDiario>
{
    public void Configure(EntityTypeBuilder<CierreDiario> builder)
    {
        builder.ToTable("cierres_diarios");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.PorcentajeCumplimiento).HasPrecision(5, 2);
        builder.Property(c => c.PorcentajePuntualidad).HasPrecision(5, 2);

        // Idempotencia: aunque dos vueltas cerraran el mismo día a la vez, la base no admite dos cierres.
        builder.HasIndex(c => new { c.OperadorId, c.Fecha }).IsUnique();
    }
}
