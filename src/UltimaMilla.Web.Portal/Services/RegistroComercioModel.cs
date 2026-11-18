using System.ComponentModel.DataAnnotations;

namespace UltimaMilla.Web.Portal.Services;

/// <summary>
/// Datos del registro de un comercio. Las reglas copian las del endpoint /api/auth/registro-comercio y las
/// de contraseña de Identity (8 caracteres, mayúscula, minúscula y número); la definitiva la hace la API.
/// </summary>
public sealed class RegistroComercioModel
{
    [Required(ErrorMessage = "Ingresá tu nombre.")]
    [StringLength(150, ErrorMessage = "El nombre admite hasta 150 caracteres.")]
    public string NombreCompleto { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ingresá tu correo.")]
    [EmailAddress(ErrorMessage = "El correo no es válido.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Elegí una contraseña.")]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{8,}$",
        ErrorMessage = "Mínimo 8 caracteres, con mayúscula, minúscula y número.")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Repetí la contraseña.")]
    [Compare(nameof(Password), ErrorMessage = "Las contraseñas no coinciden.")]
    public string ConfirmarPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ingresá el nombre del comercio.")]
    public string NombreComercio { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ingresá el RUT.")]
    public string Rut { get; set; } = string.Empty;
}
