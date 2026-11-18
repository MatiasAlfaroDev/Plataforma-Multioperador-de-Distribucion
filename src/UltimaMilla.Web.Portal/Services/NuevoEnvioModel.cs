using System.ComponentModel.DataAnnotations;

namespace UltimaMilla.Web.Portal.Services;

/// <summary>Datos del formulario de alta. Las reglas copian las de CrearEnvioValidator; la definitiva la hace la API.</summary>
public sealed class NuevoEnvioModel
{
    [Required(ErrorMessage = "Elegí la cuenta comercial.")]
    public string CuentaComercialId { get; set; } = string.Empty;

    [Required(ErrorMessage = "La referencia es obligatoria.")]
    [StringLength(64, ErrorMessage = "La referencia admite hasta 64 caracteres.")]
    public string ReferenciaExterna { get; set; } = string.Empty;

    public string Modalidad { get; set; } = "Estandar";

    [Required(ErrorMessage = "El nombre del destinatario es obligatorio.")]
    [StringLength(200, ErrorMessage = "El nombre admite hasta 200 caracteres.")]
    public string DestinatarioNombre { get; set; } = string.Empty;

    [Phone(ErrorMessage = "El teléfono no es válido.")]
    [StringLength(30, ErrorMessage = "El teléfono admite hasta 30 caracteres.")]
    public string? DestinatarioTelefono { get; set; }

    [EmailAddress(ErrorMessage = "El correo no es válido.")]
    [StringLength(200, ErrorMessage = "El correo admite hasta 200 caracteres.")]
    public string? DestinatarioEmail { get; set; }

    [Required(ErrorMessage = "La calle es obligatoria.")]
    [StringLength(200, ErrorMessage = "La calle admite hasta 200 caracteres.")]
    public string Calle { get; set; } = string.Empty;

    [StringLength(20, ErrorMessage = "El número admite hasta 20 caracteres.")]
    public string Numero { get; set; } = string.Empty;

    [Required(ErrorMessage = "La localidad es obligatoria.")]
    [StringLength(100, ErrorMessage = "La localidad admite hasta 100 caracteres.")]
    public string Localidad { get; set; } = string.Empty;

    [Required(ErrorMessage = "Elegí el departamento.")]
    public string Departamento { get; set; } = string.Empty;

    public List<NuevoBultoModel> Bultos { get; set; } =
    [
        new()
    ];
}
public class NuevoBultoModel
{
    [Range(0.001, 1000, ErrorMessage = "El peso debe estar entre 0,001 y 1000 kg.")]
    public decimal PesoKg { get; set; } = 1;

    [Range(1, 500, ErrorMessage = "El alto debe estar entre 1 y 500 cm.")]
    public int AltoCm { get; set; } = 20;

    [Range(1, 500, ErrorMessage = "El ancho debe estar entre 1 y 500 cm.")]
    public int AnchoCm { get; set; } = 20;

    [Range(1, 500, ErrorMessage = "La profundidad debe estar entre 1 y 500 cm.")]
    public int ProfundidadCm { get; set; } = 20;
}
