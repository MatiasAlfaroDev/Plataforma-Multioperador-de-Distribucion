using UltimaMilla.Domain.Common;

namespace UltimaMilla.Domain.Avisos;

/// <summary>
/// Suscripción de una cuenta comercial a los avisos HTTP (webhooks) de ciertos tipos de evento.
/// </summary>
public sealed class SuscripcionAviso : AuditableEntity, ITenantEntity
{
    /// <summary>
    /// Longitud máxima permitida para la URL del aviso.
    /// </summary>
    public const int LargoMaximoUrl = 500;

    public Guid OperadorId { get; private set; }

    public Guid CuentaComercialId { get; private set; }

    public string Url { get; private set; } = string.Empty;

    public string Secreto { get; private set; } = string.Empty;

    public List<string> TiposEvento { get; private set; } = [];

    public bool Activa { get; private set; }

    // Constructor vacío que necesita EF Core.
    private SuscripcionAviso() { }

    /// <summary>
    /// Crea una nueva suscripción a avisos.
    /// </summary>
    public static SuscripcionAviso Crear(
        Guid operadorId,
        Guid cuentaComercialId,
        string url,
        string secreto,
        IEnumerable<string> tiposEvento)
    {
        if (operadorId == Guid.Empty)
            throw new ArgumentException("El operador es obligatorio.", nameof(operadorId));

        if (cuentaComercialId == Guid.Empty)
            throw new ArgumentException("La cuenta comercial es obligatoria.", nameof(cuentaComercialId));

        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("La URL del aviso es obligatoria.", nameof(url));

        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            throw new ArgumentException("La URL del aviso debe ser una dirección http o https válida.", nameof(url));

        if (url.Trim().Length > LargoMaximoUrl)
            throw new ArgumentException($"La URL del aviso no puede exceder {LargoMaximoUrl} caracteres.", nameof(url));

        if (string.IsNullOrWhiteSpace(secreto))
            throw new ArgumentException("El secreto es obligatorio.", nameof(secreto));

        var tipos = tiposEvento.Distinct().ToList();

        if (tipos.Count == 0)
            throw new ArgumentException("Hay que elegir al menos un tipo de evento.", nameof(tiposEvento));

        foreach (var tipo in tipos)
        {
            if (!TiposAviso.Todos.Contains(tipo))
                throw new ArgumentException($"Tipo de evento desconocido: {tipo}.", nameof(tiposEvento));
        }

        return new SuscripcionAviso
        {
            Id = Guid.NewGuid(),
            OperadorId = operadorId,
            CuentaComercialId = cuentaComercialId,
            Url = url.Trim(),
            Secreto = secreto,
            TiposEvento = tipos,
            Activa = true
        };
    }

    /// <summary>
    /// Determina si esta suscripción escucha un tipo de evento específico.
    /// </summary>
    public bool Escucha(string tipoEvento) => Activa && TiposEvento.Contains(tipoEvento);

    /// <summary>
    /// Desactiva la suscripción.
    /// </summary>
    public void Desactivar() => Activa = false;
}
