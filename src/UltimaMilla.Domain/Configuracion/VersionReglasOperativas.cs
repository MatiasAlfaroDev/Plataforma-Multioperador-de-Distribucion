
using UltimaMilla.Domain.Common;

namespace UltimaMilla.Domain.Configuracion;

public enum AccionBultosPendientes
{
    NuevaVisita,
    DevolverAlComercio
}

public class VersionReglasOperativas : AuditableEntity, ITenantEntity
{
    public Guid OperadorId { get; private set; }

    public int MaximoIntentosEntrega { get; private set; }
    public int HorasMinimasEntreIntentos { get; private set; }
    public bool RequierePruebaEntrega { get; private set; }
    public int DiasMaximosParaDevolucion { get; private set; }

    public bool AdmiteEntregaParcial { get; private set; }

    public AccionBultosPendientes AccionBultosPendientes { get; private set; }

    public bool NuevaVisitaConRecargo { get; private set; }

    public DateTimeOffset VigenciaDesde { get; private set; }
    public DateTimeOffset? VigenciaHasta { get; private set; }

    private VersionReglasOperativas() { }

    public static VersionReglasOperativas Crear(
        Guid operadorId,
        int maximoIntentosEntrega,
        int horasMinimasEntreIntentos,
        bool requierePruebaEntrega,
        int diasMaximosParaDevolucion,
        DateTimeOffset vigenciaDesde,
        bool admiteEntregaParcial = false,
        AccionBultosPendientes accionBultosPendientes = AccionBultosPendientes.NuevaVisita,
        bool nuevaVisitaConRecargo = false)
    {
        if (operadorId == Guid.Empty)
            throw new ArgumentException("El operador es obligatorio.", nameof(operadorId));

        if (maximoIntentosEntrega < 1)
            throw new ArgumentException("Debe permitirse al menos un intento de entrega.");

        if (horasMinimasEntreIntentos < 0)
            throw new ArgumentException("El intervalo entre intentos no puede ser negativo.");

        if (diasMaximosParaDevolucion < 1)
            throw new ArgumentException("El plazo de devolución debe ser de al menos un día.",
                nameof(diasMaximosParaDevolucion));

        if (!Enum.IsDefined(accionBultosPendientes))
            throw new ArgumentException("La acción para los bultos pendientes no es válida.");

        if (nuevaVisitaConRecargo &&
            accionBultosPendientes != AccionBultosPendientes.NuevaVisita)
        {
            throw new ArgumentException(
                "El recargo solamente puede aplicarse a una nueva visita.");
        }

        return new VersionReglasOperativas
        {
            Id = Guid.NewGuid(),
            OperadorId = operadorId,
            MaximoIntentosEntrega = maximoIntentosEntrega,
            HorasMinimasEntreIntentos = horasMinimasEntreIntentos,
            RequierePruebaEntrega = requierePruebaEntrega,
            DiasMaximosParaDevolucion = diasMaximosParaDevolucion,
            AdmiteEntregaParcial = admiteEntregaParcial,
            AccionBultosPendientes = accionBultosPendientes,
            NuevaVisitaConRecargo = nuevaVisitaConRecargo,
            VigenciaDesde = vigenciaDesde
        };
    }

    public bool EstaVigente(DateTimeOffset fecha)
    {
        return fecha >= VigenciaDesde &&
               (VigenciaHasta is null || fecha < VigenciaHasta);
    }

    public void FinalizarVigencia(DateTimeOffset fecha)
    {
        if (fecha <= VigenciaDesde)
            throw new ArgumentException(
                "La fecha de fin debe ser posterior al inicio de vigencia.",
                nameof(fecha));

        VigenciaHasta = fecha;
    }
}
