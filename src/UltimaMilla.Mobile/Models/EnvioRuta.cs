namespace UltimaMilla.Mobile.Models;

/// <summary>
/// Un envío de la hoja de ruta tal como se guarda en el teléfono (tabla envios_ruta).
/// Se diferencia de EnvioItem (lo que manda la API) porque además guarda lo que el
/// repartidor registró sin conexión y si eso ya se sincronizó.
/// </summary>
public sealed class EnvioRuta
{
    public Guid Id { get; set; }
    public Guid OperadorId { get; set; }
    public string ReferenciaExterna { get; set; } = string.Empty;
    public string DestinatarioNombre { get; set; } = string.Empty;
    public string Direccion { get; set; } = string.Empty;
    public string ComercioNombre { get; set; } = string.Empty;
    public string Modalidad { get; set; } = string.Empty;
    public int CantidadBultos { get; set; }

    /// <summary>Estado según la API la última vez que se descargó la ruta.</summary>
    public string EstadoServidor { get; set; } = string.Empty;

    /// <summary>"Entregado" o "NoEntregado" si el repartidor ya registró el resultado en el teléfono.</summary>
    public string? ResultadoLocal { get; set; }

    /// <summary>True mientras el resultado registrado no llegó al servidor.</summary>
    public bool PendienteDeSincronizar { get; set; }

    public bool PuedeRegistrar => ResultadoLocal is null;
    
    // Un registro rechazado por el servidor no cuenta: gana lo que dice el servidor.
    public bool EsEntregado => ResultadoLocal == "Entregado" && !Rechazado;
    public bool EsNoEntregado => ResultadoLocal == "NoEntregado" && !Rechazado;

    public string EstadoMostrado => ResultadoLocal switch
    {
        null => EstadoServidor,
        "NoEntregado" => PendienteDeSincronizar ? "No entregado · pendiente de sincronizar" : "No entregado",
        _ => PendienteDeSincronizar ? "Entregado · pendiente de sincronizar" : "Entregado"
    };

    // ----- Solo para la pantalla: textos y colores ya resueltos, así el XAML queda simple -----

    /// <summary>Texto de la etiqueta de color de la tarjeta. El conflicto tiene prioridad.</summary>
    public string EtiquetaEstado =>
        Rechazado ? "Rechazado"
        : ResultadoLocal switch
        {
            null => "Por entregar",
            "NoEntregado" => "No entregado",
            _ => "Entregado"
        };

    /// <summary>Color fuerte del estado: franja lateral y texto de la etiqueta.</summary>
    public Color ColorEstado =>
        Rechazado ? Color.FromArgb("#C5221F")
        : ResultadoLocal switch
        {
            null => Color.FromArgb("#1F5FAD"),
            "NoEntregado" => Color.FromArgb("#C5221F"),
            _ => Color.FromArgb("#1E8E3E")
        };

    /// <summary>Color suave del estado: fondo de la etiqueta.</summary>
    public Color ColorEstadoSuave =>
        Rechazado ? Color.FromArgb("#FCE8E6")
        : ResultadoLocal switch
        {
            null => Color.FromArgb("#DCE8F7"),
            "NoEntregado" => Color.FromArgb("#FCE8E6"),
            _ => Color.FromArgb("#E6F4EA")
        };

    /// <summary>La línea de sincronización se muestra solo si ya se registró algo.</summary>
    public bool MostrarSincronizacion => ResultadoLocal is not null;

    public string TextoSincronizacion =>
        Rechazado ? $"⚠ Rechazado por el servidor: {UltimoError}"
        : PendienteDeSincronizar ? "⏳ Guardado en el teléfono · falta enviarlo"
        : "✓ Enviado al servidor";

    public Color ColorSincronizacion =>
        Rechazado ? Color.FromArgb("#C5221F")
        : PendienteDeSincronizar ? Color.FromArgb("#B05A00")
        : Color.FromArgb("#1E8E3E");

    /// <summary>Por qué el servidor rechazó lo registrado (null si nunca lo rechazó).</summary>
    public string? UltimoError { get; set; }

    /// <summary>Conflicto: falta sincronizar y el servidor ya dijo que no.</summary>
    public bool Rechazado => PendienteDeSincronizar && UltimoError is not null;

    public string DetalleBultos => $"{CantidadBultos} {(CantidadBultos == 1 ? "bulto" : "bultos")} · {Modalidad}";
}
