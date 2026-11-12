namespace UltimaMilla.Domain.Rutas;

public enum EstadoHojaDeRuta
{
    EnPreparacion,
    Despachada,

    /// <summary>RF 22: el repartidor volvió al depósito, se rindió lo que traía y la hoja quedó cerrada.</summary>
    Rendida
}