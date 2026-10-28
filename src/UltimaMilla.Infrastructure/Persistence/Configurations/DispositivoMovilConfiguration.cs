using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UltimaMilla.Domain.Notificaciones;
using UltimaMilla.Infrastructure.Identidad;

namespace UltimaMilla.Infrastructure.Persistence.Configurations;

/// <summary>Teléfonos registrados para notificaciones push.</summary>
public sealed class DispositivoMovilConfiguration : IEntityTypeConfiguration<DispositivoMovil>
{
    public void Configure(EntityTypeBuilder<DispositivoMovil> builder)
    {
        builder.ToTable("dispositivos_moviles");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.Token)
            .HasMaxLength(DispositivoMovil.LargoMaximoToken)
            .IsRequired();

        builder.Property(d => d.Plataforma)
            .HasMaxLength(16)
            .IsRequired();

        // Si se borra el usuario, se borran sus teléfonos.
        builder.HasOne<Usuario>()
            .WithMany()
            .HasForeignKey(d => d.UsuarioId)
            .OnDelete(DeleteBehavior.Cascade);

        // Un token aparece una sola vez por operador: volver a registrarlo lo renueva, no lo duplica.
        builder.HasIndex(d => new { d.OperadorId, d.Token }).IsUnique();

        // Para buscar "los teléfonos de este repartidor" (ADR-002: el índice empieza por OperadorId).
        builder.HasIndex(d => new { d.OperadorId, d.UsuarioId });
    }
}
