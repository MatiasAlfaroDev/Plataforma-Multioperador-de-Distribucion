namespace UltimaMilla.Web.Portal.Services;

/// <summary>
/// Toasts del Portal: las páginas avisan y <c>MainLayout</c> los muestra. Es scoped, así que hay uno
/// por circuito. Los de éxito se cierran solos; los de error quedan hasta que el usuario los cierra.
/// </summary>
public sealed class Notificaciones
{
    public sealed record Notificacion(Guid Id, bool Exito, string Titulo, string Mensaje);

    private static readonly TimeSpan DuracionExito = TimeSpan.FromSeconds(6);
    private readonly List<Notificacion> lista = [];

    public event Action? Cambio;

    // El cierre automático llega desde otro hilo; por eso se copia la lista bajo el lock.
    public IReadOnlyList<Notificacion> Lista { get { lock (lista) return [.. lista]; } }

    public void Exito(string titulo, string mensaje)
    {
        var n = Agregar(true, titulo, mensaje);
        _ = Task.Delay(DuracionExito).ContinueWith(_ => Cerrar(n.Id), TaskScheduler.Default);
    }

    public void Error(string titulo, string mensaje) => Agregar(false, titulo, mensaje);

    public void Cerrar(Guid id)
    {
        lock (lista) lista.RemoveAll(n => n.Id == id);
        Cambio?.Invoke();
    }

    private Notificacion Agregar(bool exito, string titulo, string mensaje)
    {
        var n = new Notificacion(Guid.NewGuid(), exito, titulo, mensaje);
        lock (lista) lista.Add(n);
        Cambio?.Invoke();
        return n;
    }
}
