
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UltimaMilla.Domain.Envios;
using UltimaMilla.Domain.Organizacion;
using UltimaMilla.Domain.Rutas;
using UltimaMilla.Infrastructure.Identidad;

namespace UltimaMilla.Infrastructure.Persistence.Configurations;

public sealed class RepartidorConfiguration
    : IEntityTypeConfiguration<Repartidor>
{
    public void Configure(EntityTypeBuilder<Repartidor> builder)
    {
        builder.ToTable("repartidores");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Nombre)
            .HasMaxLength(150)
            .IsRequired();
        builder.Property(x => x.Telefono)
            .HasMaxLength(30);

        builder.HasOne<Operador>()
            .WithMany()
            .HasForeignKey(x => x.OperadorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.OperadorId, x.Activo });

        // Si se borra el usuario, el repartidor queda sin cuenta (no se borra: tiene rutas).
        builder.HasOne<Usuario>()
            .WithMany()
            .HasForeignKey(x => x.UsuarioId)
            .OnDelete(DeleteBehavior.SetNull);

        // Una cuenta es de un solo repartidor por operador.
        builder.HasIndex(x => new { x.OperadorId, x.UsuarioId })
            .IsUnique()
            .HasFilter("\"UsuarioId\" IS NOT NULL");
    }
}

public sealed class VehiculoConfiguration
    : IEntityTypeConfiguration<Vehiculo>
{
    public void Configure(EntityTypeBuilder<Vehiculo> builder)
    {
        builder.ToTable("vehiculos");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Matricula)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.CapacidadPesoKg)
            .HasPrecision(12, 3);

        builder.Property(x => x.CapacidadVolumenM3)
            .HasPrecision(12, 6);

        builder.HasOne<Operador>()
            .WithMany()
            .HasForeignKey(x => x.OperadorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.OperadorId, x.Matricula })
            .IsUnique();
    }
}

public sealed class HojaDeRutaConfiguration
    : IEntityTypeConfiguration<HojaDeRuta>
{
    public void Configure(EntityTypeBuilder<HojaDeRuta> builder)
    {
        builder.ToTable("hojas_de_ruta");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.Estado)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.DespachadaEn)
            .HasColumnType("timestamp with time zone");

        builder.Property(x => x.CapacidadPesoKg)
            .HasPrecision(12, 3);

        builder.Property(x => x.CapacidadVolumenM3)
            .HasPrecision(12, 6);

        builder.Property(x => x.PesoAsignadoKg)
            .HasPrecision(12, 3);

        builder.Property(x => x.VolumenAsignadoM3)
            .HasPrecision(12, 6);

        builder.HasOne<Operador>()
            .WithMany()
            .HasForeignKey(x => x.OperadorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Repartidor>()
            .WithMany()
            .HasForeignKey(x => x.RepartidorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Vehiculo>()
            .WithMany()
            .HasForeignKey(x => x.VehiculoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Paradas)
            .WithOne()
            .HasForeignKey(x => x.HojaDeRutaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Navigation(x => x.Paradas)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(x => new { x.OperadorId, x.Fecha });
    }
}

public sealed class ParadaRutaConfiguration
    : IEntityTypeConfiguration<ParadaRuta>
{
    public void Configure(EntityTypeBuilder<ParadaRuta> builder)
    {
        builder.ToTable("paradas_ruta");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.LiberadaEn)
            .HasColumnName("liberada_en");

        // Es una propiedad calculada, no una columna.
        builder.Ignore(x => x.Activa);

        builder.HasOne<Envio>()
            .WithMany()
            .HasForeignKey(x => x.EnvioId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.HojaDeRutaId, x.Orden })
            .IsUnique()
            .HasFilter("\"liberada_en\" IS NULL");

        // Impide que un mismo envío tenga dos
        // asignaciones activas al mismo tiempo.
        builder.HasIndex(x => x.EnvioId)
            .IsUnique()
            .HasFilter("\"liberada_en\" IS NULL");
    }
}
