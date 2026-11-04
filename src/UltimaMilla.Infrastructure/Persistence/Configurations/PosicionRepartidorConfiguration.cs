using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UltimaMilla.Domain.Organizacion;
using UltimaMilla.Domain.Rutas;

namespace UltimaMilla.Infrastructure.Persistence.Configurations;

/// <summary>Recorrido de los repartidores (RF 23).</summary>
public sealed class PosicionRepartidorConfiguration : IEntityTypeConfiguration<PosicionRepartidor>
{
    public void Configure(EntityTypeBuilder<PosicionRepartidor> builder)
    {
        builder.ToTable("posiciones_repartidor");

        // La clave es el Id que generó el teléfono: un reenvío no duplica posiciones (idempotencia).
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.Latitud).HasPrecision(9, 6);
        builder.Property(p => p.Longitud).HasPrecision(9, 6);

        builder.HasOne<Repartidor>().WithMany().HasForeignKey(p => p.RepartidorId).OnDelete(DeleteBehavior.Restrict);

        // "El recorrido de este repartidor hoy": empieza por OperadorId (ADR-002).
        builder.HasIndex(p => new { p.OperadorId, p.RepartidorId, p.RegistradaEn });
    }
}
