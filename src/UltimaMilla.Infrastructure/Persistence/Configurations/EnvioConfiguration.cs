using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UltimaMilla.Domain.Envios;
using UltimaMilla.Domain.Organizacion;

namespace UltimaMilla.Infrastructure.Persistence.Configurations;

/// <summary>Cómo se traduce el agregado Envio a tablas.</summary>
public sealed class EnvioConfiguration : IEntityTypeConfiguration<Envio>
{
    public void Configure(EntityTypeBuilder<Envio> builder)
    {
        builder.ToTable("envios");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.ReferenciaExterna).HasMaxLength(64).IsRequired();
        builder.Property(e => e.Modalidad).HasConversion<string>().HasMaxLength(16);
        builder.Property(e => e.Estado).HasConversion<string>().HasMaxLength(32);
        builder.Property(e => e.TarifaCalculada).HasPrecision(12, 2);
        builder.Ignore(e => e.PesoTotalKg);

        // Concurrencia optimista con la columna de sistema xmin de PostgreSQL (informe 6.1 y 6.4).
        builder.Property<uint>("Version").IsRowVersion();

        // Destinatario y Dirección son objetos de valor: columnas de la misma tabla.
        builder.OwnsOne(e => e.Destinatario, d =>
        {
            d.Property(x => x.Nombre).HasColumnName("destinatario_nombre").HasMaxLength(200).IsRequired();
            d.Property(x => x.Telefono).HasColumnName("destinatario_telefono").HasMaxLength(30);
        });
        builder.Navigation(e => e.Destinatario).IsRequired();

        builder.OwnsOne(e => e.Direccion, d =>
        {
            d.Property(x => x.Calle).HasColumnName("direccion_calle").HasMaxLength(200).IsRequired();
            d.Property(x => x.Numero).HasColumnName("direccion_numero").HasMaxLength(20);
            d.Property(x => x.Localidad).HasColumnName("direccion_localidad").HasMaxLength(100).IsRequired();
            d.Property(x => x.Departamento).HasColumnName("direccion_departamento").HasMaxLength(50).IsRequired();
        });
        builder.Navigation(e => e.Direccion).IsRequired();

        // Bultos: tabla propia, se guardan y se borran junto con el envío.
        builder.HasMany(e => e.Bultos).WithOne().HasForeignKey("EnvioId").OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(e => e.Bultos).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasOne<CuentaComercial>().WithMany().HasForeignKey(e => e.CuentaComercialId).OnDelete(DeleteBehavior.Restrict);

        // Importación idempotente (RF 7): la referencia es única dentro de cada cuenta comercial.
        builder.HasIndex(e => new { e.CuentaComercialId, e.ReferenciaExterna }).IsUnique();
        // Las consultas por inquilino empiezan por OperadorId (ADR-002, vecino ruidoso).
        builder.HasIndex(e => new { e.OperadorId, e.FechaAlta });
    }
}

public sealed class BultoConfiguration : IEntityTypeConfiguration<Bulto>
{
    public void Configure(EntityTypeBuilder<Bulto> builder)
    {
        builder.ToTable("bultos");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Identificacion).HasMaxLength(80).IsRequired();
        builder.Property(b => b.PesoKg).HasPrecision(10, 3);
    }
}
