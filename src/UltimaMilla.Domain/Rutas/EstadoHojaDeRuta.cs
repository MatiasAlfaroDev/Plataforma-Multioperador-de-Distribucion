namespace UltimaMilla.Domain.Rutas;

public enum EstadoHojaDeRuta
{
    EnPreparacion,
    Despachada,

    /// <summary>RF 22: el repartidor volvió y rindió desde la app lo que trae de vuelta; falta el control del depósito.</summary>
    PendienteDeControl,

    /// <summary>RF 22: el depósito controló la rendición en el Backoffice, recibió lo devuelto y la hoja quedó cerrada.</summary>
    Rendida
}