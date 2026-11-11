using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UltimaMilla.Domain.Organizacion;
using UltimaMilla.Domain.Retiros;
using UltimaMilla.Domain.Configuracion;

namespace UltimaMilla.Infrastructure.Persistence.Configurations;

public sealed class RetiroConfiguration : IEntityTypeConfiguration<Retiro>
{
    public void Configure(EntityTypeBuilder<Retiro> builder)
    {
        builder.ToTable("retiros");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Motivo)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(r => r.Estado)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(r => r.ReferenciaFotografia)
            .HasMaxLength(500);

        builder.Property(r => r.ReferenciaFirma)
            .HasMaxLength(500);

        builder.Ignore(r => r.PesoTotalKg);
        builder.Ignore(r => r.VolumenTotalMetrosCubicos);

        // Concurrencia optimista, igual que Envio.
        builder.Property<uint>("Version").IsRowVersion();

        builder.OwnsOne(r => r.DomicilioRetiro, d =>
        {
            d.Property(x => x.Calle)
                .HasColumnName("retiro_calle")
                .HasMaxLength(200)
                .IsRequired();

            d.Property(x => x.Numero)
                .HasColumnName("retiro_numero")
                .HasMaxLength(20);

            d.Property(x => x.Localidad)
                .HasColumnName("retiro_localidad")
                .HasMaxLength(100)
                .IsRequired();

            d.Property(x => x.Departamento)
                .HasColumnName("retiro_departamento")
                .HasMaxLength(50)
                .IsRequired();
        });

        builder.Navigation(r => r.DomicilioRetiro).IsRequired();

        builder.OwnsOne(r => r.DomicilioDestino, d =>
        {
            d.Property(x => x.Calle)
                .HasColumnName("destino_calle")
                .HasMaxLength(200)
                .IsRequired();

            d.Property(x => x.Numero)
                .HasColumnName("destino_numero")
                .HasMaxLength(20);

            d.Property(x => x.Localidad)
                .HasColumnName("destino_localidad")
                .HasMaxLength(100)
                .IsRequired();

            d.Property(x => x.Departamento)
                .HasColumnName("destino_departamento")
                .HasMaxLength(50)
                .IsRequired();
        });

        builder.Navigation(r => r.DomicilioDestino).IsRequired();

        builder.HasMany(r => r.Bultos)
            .WithOne()
            .HasForeignKey("RetiroId")
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(r => r.Bultos)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasOne<CuentaComercial>()
            .WithMany()
            .HasForeignKey(r => r.CuentaComercialId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => new
        {
            r.OperadorId,
            r.SolicitadoEn
        });

        builder.HasIndex(r => new
        {
            r.OperadorId,
            r.CuentaComercialId,
            r.Estado
        });

        builder.Property(r => r.TarifaCalculada)
            .HasPrecision(12, 2);

        builder.HasOne<VersionTarifaria>()
            .WithMany()
            .HasForeignKey(r => r.VersionTarifariaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}