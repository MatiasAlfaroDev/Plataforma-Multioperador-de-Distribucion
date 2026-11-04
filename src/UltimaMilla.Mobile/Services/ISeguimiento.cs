namespace UltimaMilla.Mobile.Services;

/// <summary>
/// Seguimiento de la posición durante la jornada (RF 23 y 6.11), con la app en segundo plano
/// y la pantalla apagada. Arranca al despachar la ruta y termina con "Terminar jornada".
/// En Android lo implementa SeguimientoAndroid (un servicio en primer plano).
/// </summary>
public interface ISeguimiento
{
    /// <summary>La jornada está en curso (queda guardado aunque se cierre la app).</summary>
    bool Activo { get; }

    /// <summary>Pide el permiso de ubicación y arranca el seguimiento. False si no se pudo.</summary>
    Task<bool> IniciarAsync();

    void Detener();
}

/// <summary>Windows, iOS y Mac: sin seguimiento en segundo plano por ahora.</summary>
public sealed class SeguimientoNulo : ISeguimiento
{
    public bool Activo => false;

    public Task<bool> IniciarAsync() => Task.FromResult(false);

    public void Detener() { }
}
