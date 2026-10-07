using System.ComponentModel.DataAnnotations;

namespace UltimaMilla.Web.Portal.Services;

/// <summary>Datos del formulario de alta. La validación definitiva la hace la API.</summary>
public sealed class NuevoEnvioModel
{
    [Required(ErrorMessage = "Elegí la cuenta comercial.")]
    public string CuentaComercialId { get; set; } = string.Empty;

    [Required(ErrorMessage = "La referencia es obligatoria.")]
    [StringLength(64)]
    public string ReferenciaExterna { get; set; } = string.Empty;

    public string Modalidad { get; set; } = "Estandar";

    [Required(ErrorMessage = "El nombre del destinatario es obligatorio.")]
    [StringLength(200)]
    public string DestinatarioNombre { get; set; } = string.Empty;

    [StringLength(30)]
    public string? DestinatarioTelefono { get; set; }

    [Required(ErrorMessage = "La calle es obligatoria.")]
    public string Calle { get; set; } = string.Empty;

    public string Numero { get; set; } = string.Empty;

    [Required(ErrorMessage = "La localidad es obligatoria.")]
    public string Localidad { get; set; } = string.Empty;

    [Required(ErrorMessage = "El departamento es obligatorio.")]
    public string Departamento { get; set; } = "Montevideo";

    public List<NuevoBultoModel> Bultos { get; set; } =
    [
        new()
    ];
}
public sealed class NuevoBultoModel
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
