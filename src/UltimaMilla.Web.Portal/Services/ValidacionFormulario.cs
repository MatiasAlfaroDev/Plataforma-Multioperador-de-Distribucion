using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Components.Forms;

namespace UltimaMilla.Web.Portal.Services;

/// <summary>Estado de una tarjeta del formulario en el resumen: "mal" si tiene errores, "ok" si lo obligatorio está completo.</summary>
public sealed record SeccionResumen(string Nombre, string Estado, IReadOnlyList<string> Errores);

/// <summary>
/// Validación de los formularios con lista de bultos (Nuevo envío, Retiros). DataAnnotationsValidator valida un
/// campo del bulto al cambiarlo, pero al enviar recorre solo el modelo raíz: sin esto, un bulto fuera de rango
/// llegaba a la API.
/// </summary>
public static class ValidacionFormulario
{
    public static EditContext Crear(
        object modelo,
        Func<IEnumerable<object>> elementos,
        Action repintar,
        Action<ValidationMessageStore>? reglasExtra = null)
    {
        var contexto = new EditContext(modelo);
        var errores = new ValidationMessageStore(contexto);

        // Al editar un campo se borra su error viejo; el nuevo lo pone DataAnnotationsValidator.
        contexto.OnFieldChanged += (_, e) => errores.Clear(e.FieldIdentifier);
        contexto.OnValidationRequested += (_, _) =>
        {
            errores.Clear();
            foreach (var elemento in elementos())
            {
                var resultados = new List<ValidationResult>();
                Validator.TryValidateObject(elemento, new ValidationContext(elemento), resultados, validateAllProperties: true);
                foreach (var r in resultados)
                    foreach (var campo in r.MemberNames)
                        errores.Add(new FieldIdentifier(elemento, campo), r.ErrorMessage!);
            }
            reglasExtra?.Invoke(errores);
        };
        // El resumen lee los errores en el render de la página, que no se repinta sola cuando cambia la validación.
        contexto.OnValidationStateChanged += (_, _) => repintar();
        return contexto;
    }

    // Las secciones se arman campo por campo, y no con GetValidationMessages(), para que un bulto ya
    // quitado no deje su error en el resumen.
    public static SeccionResumen Seccion(EditContext contexto, string nombre, string[] campos, params string?[] obligatorios)
    {
        var errores = campos.SelectMany(c => contexto.GetValidationMessages(new FieldIdentifier(contexto.Model, c))).ToList();
        var estado = errores.Count > 0 ? "mal" : obligatorios.All(v => !string.IsNullOrWhiteSpace(v)) ? "ok" : "pendiente";
        return new SeccionResumen(nombre, estado, errores);
    }

    public static SeccionResumen SeccionBultos(EditContext contexto, IEnumerable<object> bultos, string[] campos)
    {
        var errores = bultos.SelectMany((b, i) => campos.SelectMany(c =>
            contexto.GetValidationMessages(new FieldIdentifier(b, c)).Select(m => $"Bulto {i + 1}: {m}"))).ToList();
        return new SeccionResumen("Bultos", errores.Count > 0 ? "mal" : "ok", errores);
    }
}
