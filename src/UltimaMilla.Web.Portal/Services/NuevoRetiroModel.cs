using System.ComponentModel.DataAnnotations;

namespace UltimaMilla.Web.Portal.Services;

/// <summary>Datos del formulario de retiro. Las reglas copian las de CrearRetiroValidator; la definitiva la hace la API.</summary>
public sealed class NuevoRetiroModel
{
    [Required(ErrorMessage = "Elegí el operador.")]
    public string CuentaComercialId { get; set; } = string.Empty;

    [Required(ErrorMessage = "El motivo es obligatorio.")]
    [StringLength(500, ErrorMessage = "El motivo admite hasta 500 caracteres.")]
    public string Motivo { get; set; } = string.Empty;

    public DateOnly Fecha { get; set; }

    public TimeOnly HoraDesde { get; set; } = new(9, 0);

    public TimeOnly HoraHasta { get; set; } = new(13, 0);

    [Required(ErrorMessage = "La calle es obligatoria.")]
    [StringLength(200, ErrorMessage = "La calle admite hasta 200 caracteres.")]
    public string Calle { get; set; } = string.Empty;

    [StringLength(20, ErrorMessage = "El número admite hasta 20 caracteres.")]
    public string Numero { get; set; } = string.Empty;

    [Required(ErrorMessage = "La localidad es obligatoria.")]
    [StringLength(100, ErrorMessage = "La localidad admite hasta 100 caracteres.")]
    public string Localidad { get; set; } = string.Empty;

    [Required(ErrorMessage = "El departamento es obligatorio.")]
    [StringLength(50, ErrorMessage = "El departamento admite hasta 50 caracteres.")]
    public string Departamento { get; set; } = string.Empty;

    public List<NuevoBultoRetiroModel> Bultos { get; set; } = [new()];
}

public sealed class NuevoBultoRetiroModel : NuevoBultoModel
{
    [Required(ErrorMessage = "La identificación es obligatoria.")]
    [StringLength(100, ErrorMessage = "La identificación admite hasta 100 caracteres.")]
    public string Identificacion { get; set; } = string.Empty;
}
