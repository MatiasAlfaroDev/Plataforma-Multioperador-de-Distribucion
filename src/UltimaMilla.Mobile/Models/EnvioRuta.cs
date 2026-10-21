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

    public string EstadoMostrado => ResultadoLocal switch
    {
        null => EstadoServidor,
        "NoEntregado" => PendienteDeSincronizar ? "No entregado · pendiente de sincronizar" : "No entregado",
        _ => PendienteDeSincronizar ? "Entregado · pendiente de sincronizar" : "Entregado"
    };
}
