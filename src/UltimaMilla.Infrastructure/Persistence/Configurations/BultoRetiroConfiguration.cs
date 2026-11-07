using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UltimaMilla.Domain.Retiros;

namespace UltimaMilla.Infrastructure.Persistence.Configurations;

public sealed class BultoRetiroConfiguration
    : IEntityTypeConfiguration<BultoRetiro>
{
    public void Configure(EntityTypeBuilder<BultoRetiro> builder)
    {
        builder.ToTable("bultos_retiro");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.Identificacion)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(b => b.PesoKg)
            .HasPrecision(10, 3);

        builder.Ignore(b => b.VolumenMetrosCubicos);

        builder.HasIndex("RetiroId", nameof(BultoRetiro.Identificacion))
            .IsUnique();
    }
}