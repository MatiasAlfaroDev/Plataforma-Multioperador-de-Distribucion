using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UltimaMilla.Domain.Organizacion;
using UltimaMilla.Infrastructure.Identidad;

namespace UltimaMilla.Infrastructure.Persistence.Configurations;

public sealed class MembresiaUsuarioConfiguration
    : IEntityTypeConfiguration<MembresiaUsuario>
{
    public void Configure(EntityTypeBuilder<MembresiaUsuario> builder)
    {
        builder.ToTable("membresias_usuarios");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Perfil)
            .HasMaxLength(50)
            .IsRequired();

        builder.HasOne(m => m.Usuario)
            .WithMany()
            .HasForeignKey(m => m.UsuarioId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Operador>()
            .WithMany()
            .HasForeignKey(m => m.OperadorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<CuentaComercial>()
            .WithMany()
            .HasForeignKey(m => m.CuentaComercialId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(m => new
        {
            m.UsuarioId,
            m.OperadorId,
            m.CuentaComercialId,
            m.Perfil
        }).IsUnique();
    }
}