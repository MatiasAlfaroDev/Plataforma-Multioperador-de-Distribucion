
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UltimaMilla.Domain.Organizacion;
using UltimaMilla.Infrastructure.Identidad;

namespace UltimaMilla.Infrastructure.Persistence.Configurations;

public sealed class UsuarioComercioConfiguration
    : IEntityTypeConfiguration<UsuarioComercio>
{
    public void Configure(EntityTypeBuilder<UsuarioComercio> builder)
    {
        builder.ToTable("usuarios_comercios");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Perfil)
            .HasMaxLength(50)
            .IsRequired();

        builder.HasOne(x => x.Usuario)
            .WithMany()
            .HasForeignKey(x => x.UsuarioId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Comercio>()
            .WithMany()
            .HasForeignKey(x => x.ComercioId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new
        {
            x.UsuarioId,
            x.ComercioId
        }).IsUnique();

        builder.Property(x => x.ClaveApiHash).HasMaxLength(64);
        builder.HasIndex(x => x.ClaveApiHash).IsUnique();
    }
}
