namespace UltimaMilla.Infrastructure.Identidad;

public sealed class MembresiaUsuario
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UsuarioId { get; set; }

    public Guid OperadorId { get; set; }

    // Solo se utiliza cuando el perfil es Comercio.
    public Guid? CuentaComercialId { get; set; }

    public string Perfil { get; set; } = string.Empty;

    public bool Activa { get; set; } = true;

    public Usuario Usuario { get; set; } = null!;
}